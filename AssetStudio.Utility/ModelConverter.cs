﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
namespace AssetStudio
{
    public class ModelConverter : IImported
    {
        public ImportedFrame RootFrame { get; protected set; }
        public List<ImportedMesh> MeshList { get; protected set; } = new List<ImportedMesh>();
        public List<ImportedMaterial> MaterialList { get; protected set; } = new List<ImportedMaterial>();
        public List<ImportedTexture> TextureList { get; protected set; } = new List<ImportedTexture>();
        public List<ImportedKeyframedAnimation> AnimationList { get; protected set; } = new List<ImportedKeyframedAnimation>();
        public List<ImportedMorph> MorphList { get; protected set; } = new List<ImportedMorph>();

        private Options options;
        private Avatar avatar;
        private HashSet<AnimationClip> animationClipHashSet = new HashSet<AnimationClip>();
        private Dictionary<AnimationClip, string> boundAnimationPathDic = new Dictionary<AnimationClip, string>();
        private Dictionary<uint, string> bonePathHash = new Dictionary<uint, string>();
        private Dictionary<Texture2D, string> textureNameDictionary = new Dictionary<Texture2D, string>();
        private Dictionary<Transform, ImportedFrame> transformDictionary = new Dictionary<Transform, ImportedFrame>();
        Dictionary<uint, string> morphChannelNames = new Dictionary<uint, string>();

        public ModelConverter(GameObject m_GameObject, Options options, AnimationClip[] animationList = null)
        {
            this.options = options;

            if (m_GameObject.m_Animator != null)
            {
                InitWithAnimator(m_GameObject.m_Animator);
                if (animationList == null && this.options.collectAnimations)
                {
                    CollectAnimationClip(m_GameObject.m_Animator);
                }
            }
            else
            {
                InitWithGameObject(m_GameObject);
            }
            if (animationList != null)
            {
                foreach (var animationClip in animationList)
                {
                    animationClipHashSet.Add(animationClip);
                }
            }
            ConvertAnimations();
        }

        public ModelConverter(string rootName, List<GameObject> m_GameObjects, Options options, AnimationClip[] animationList = null)
        {
            this.options = options;

            RootFrame = CreateFrame(rootName, Vector3.Zero, new Quaternion(0, 0, 0, 0), Vector3.One);
            foreach (var m_GameObject in m_GameObjects)
            {
                if (m_GameObject.m_Animator != null && animationList == null && this.options.collectAnimations)
                {
                    CollectAnimationClip(m_GameObject.m_Animator);
                }

                var m_Transform = m_GameObject.m_Transform;
                ConvertTransforms(m_Transform, RootFrame);
                CreateBonePathHash(m_Transform);
            }
            foreach (var m_GameObject in m_GameObjects)
            {
                var m_Transform = m_GameObject.m_Transform;
                ConvertMeshRenderer(m_Transform);
            }
            if (animationList != null)
            {
                foreach (var animationClip in animationList)
                {
                    animationClipHashSet.Add(animationClip);
                }
            }
            ConvertAnimations();
        }

        public ModelConverter(Animator m_Animator, Options options, AnimationClip[] animationList = null)
        {
            this.options = options;

            InitWithAnimator(m_Animator);
            if (animationList == null && this.options.collectAnimations)
            {
                CollectAnimationClip(m_Animator);
            }
            else
            {
                if (animationList != null)
                {
                foreach (var animationClip in animationList)
                {
                    animationClipHashSet.Add(animationClip);
                }
            }
            }
            ConvertAnimations();
        }

        private void InitWithAnimator(Animator m_Animator)
        {
            if (m_Animator.m_Avatar.TryGet(out var m_Avatar))
                avatar = m_Avatar;

            m_Animator.m_GameObject.TryGet(out var m_GameObject);
            InitWithGameObject(m_GameObject, m_Animator.m_HasTransformHierarchy);
        }

        private void InitWithGameObject(GameObject m_GameObject, bool hasTransformHierarchy = true)
        {
            var m_Transform = m_GameObject.m_Transform;
            if (!hasTransformHierarchy)
            {
                ConvertTransforms(m_Transform, null);
                DeoptimizeTransformHierarchy();
            }
            else
            {
                var frameList = new List<ImportedFrame>();
                var tempTransform = m_Transform;
                while (tempTransform.m_Father.TryGet(out var m_Father))
                {
                    frameList.Add(ConvertTransform(m_Father));
                    tempTransform = m_Father;
                }
                if (frameList.Count > 0)
                {
                    RootFrame = frameList[frameList.Count - 1];
                    for (var i = frameList.Count - 2; i >= 0; i--)
                    {
                        var frame = frameList[i];
                        var parent = frameList[i + 1];
                        parent.AddChild(frame);
                    }
                    ConvertTransforms(m_Transform, frameList[0]);
                }
                else
                {
                    ConvertTransforms(m_Transform, null);
                }

                CreateBonePathHash(m_Transform);
            }

            ConvertMeshRenderer(m_Transform);
        }

        public class GakuBlendShape
        {
            public string BlendShapeName { get; set; }
            public List<GakuBlendShapeVertex> Vertices { get; set; }

            public GakuBlendShape(string name)
            {
                BlendShapeName = name;
                Vertices = new List<GakuBlendShapeVertex>();
            }
        }

        public class GakuBlendShapeVertex
        {
            public int VertIndex { get; set; }
            public Vector3 Position { get; set; }

            public GakuBlendShapeVertex(int vertIndex, Vector3 position)
            {
                VertIndex = vertIndex;
                Position = position;
            }
        }
        public class VLSkinningRenderer
        {
            public List<PPtr<Transform>> Bones { get; set; }
            public List<Matrix4x4> Bindposes { get; set; }
            public AABB LocalBounds { get; set; }
            public List<uint> BoneWeightAndIndices { get; set; }
            public List<GakuBlendShape> BlendShapesList { get; set; }
            public List<float> blendShapeWeightsList { get; set; }
            public static VLSkinningRenderer Instance { get; private set; } = new VLSkinningRenderer();
            public VLSkinningRenderer()
            {
                Bones = new List<PPtr<Transform>>();
                Bindposes = new List<Matrix4x4>();
                BoneWeightAndIndices = new List<uint>();
                BlendShapesList = new List<GakuBlendShape>();
                blendShapeWeightsList = new List<float>();
            }
        }
        private void ConvertMeshRenderer(Transform m_Transform)
        {
            m_Transform.m_GameObject.TryGet(out var m_GameObject);
            if (m_GameObject.m_Components.Find(x => x.Name == "VLActorFaceModel") != null || m_GameObject.Name == "VLSkinningRenderer")
            {
                var vlskinningRenderer = new VLSkinningRenderer();
                var vlskin = m_GameObject.assetsFile.ObjectsDic.FirstOrDefault(x => x.Value.Name == "VLSkinningRenderer").Value as GameObject;

                if (vlskin != null)
                {

                    PPtr<Mesh> ptrMesh = null;
                    var VLActorFaceModel = m_GameObject.assetsFile.ObjectsDic.FirstOrDefault(x => x.Value.Name == "VLActorFaceModel" && x.Value.type == ClassIDType.MonoBehaviour).Value as MonoBehaviour;
                    if (VLActorFaceModel == null)
                    {
                        Logger.Error($"VLActorFaceModel not found for {m_GameObject.Name} @ {m_GameObject.m_PathID}");
                    }
                    if (VLActorFaceModel != null)
                    {
                        var obj = VLActorFaceModel.ToType();
                        var tmp = obj["bones"] as List<object>;
                        if (tmp != null)
                        {

                            foreach (OrderedDictionary bone in tmp.OfType<OrderedDictionary>())
                            {
                                var fileID = Convert.ToInt32(bone["m_FileID"] ?? 0);
                                var pathID = Convert.ToInt64(bone["m_PathID"] ?? 0);

                                vlskinningRenderer.Bones.Add(new PPtr<Transform>(fileID, pathID, m_GameObject.assetsFile));
                            }

                        }
                        tmp = obj["bindposes"] as List<object>;
                        if (tmp != null)
                        {
                            var bindposesList = new List<Matrix4x4>();

                            foreach (OrderedDictionary dictionary in tmp.OfType<OrderedDictionary>())
                            {
                                var matrixValues = new float[16];
                                var values = dictionary.Values.Cast<float>().ToArray();
                                if (values.Length == 16)
                                {
                                    matrixValues = values;
                                }
                                else
                                {
                                    Array.Fill(matrixValues, 0f);
                                }

                                var matrix = new Matrix4x4(values);
                                bindposesList.Add(matrix);

                            }
                            vlskinningRenderer.Bindposes = bindposesList;
                        }
                        var tmp1 = obj["localBounds"] as OrderedDictionary;
                        if (tmp1 != null)
                        {
                            AABB localBounds = null;
                            var m_Center = tmp1["m_Center"] as OrderedDictionary;
                            var m_Extent = tmp1["m_Extent"] as OrderedDictionary;

                            if (m_Center != null && m_Extent != null)
                            {
                                var centerX = Convert.ToSingle(m_Center["x"]);
                                var centerY = Convert.ToSingle(m_Center["y"]);
                                var centerZ = Convert.ToSingle(m_Center["z"]);
                                var extentX = Convert.ToSingle(m_Extent["x"]);
                                var extentY = Convert.ToSingle(m_Extent["y"]);
                                var extentZ = Convert.ToSingle(m_Extent["z"]);

                                Vector3 center = new Vector3(centerX, centerY, centerZ);
                                Vector3 extent = new Vector3(extentX, extentY, extentZ);
                                localBounds = new AABB(center, extent);
                            }

                            if (localBounds != null)
                            {
                                vlskinningRenderer.LocalBounds = localBounds;
                            }
                        }
                        tmp = obj["boneWeightAndIndices"] as List<object>;
                        if (tmp != null)
                        {
                            var boneWeightAndIndices = new List<uint>();

                            foreach (var bone in tmp)
                            {
                                boneWeightAndIndices.Add(Convert.ToUInt32(bone));

                            }

                            vlskinningRenderer.BoneWeightAndIndices = boneWeightAndIndices;
                        }
                        tmp = obj["blendShapes"] as List<object>;
                        if (tmp != null)
                        {
                            var blendShapesList = new List<GakuBlendShape>();
                            foreach (OrderedDictionary blendShapeDataDict in tmp.OfType<OrderedDictionary>())
                            {

                                var blendShapeName = blendShapeDataDict["blendShapeName"] as string;
                                var blendShape = new GakuBlendShape(blendShapeName);

                                var blendShapeVerticesList = blendShapeDataDict["blendShapeVertices"] as List<object>;

                                if (blendShapeVerticesList != null)
                                {
                                    foreach (OrderedDictionary vertexData in blendShapeVerticesList.OfType<OrderedDictionary>())
                                    {
                                        int vertIndex = Convert.ToInt32(vertexData["vertIndex"]);

                                        var positionDict = vertexData["position"] as OrderedDictionary;
                                        if (positionDict != null)
                                        {
                                            float x = Convert.ToSingle(positionDict["x"]);
                                            float y = Convert.ToSingle(positionDict["y"]);
                                            float z = Convert.ToSingle(positionDict["z"]);

                                            Vector3 position = new Vector3(x, y, z);
                                            var vertex = new GakuBlendShapeVertex(vertIndex, position);
                                            blendShape.Vertices.Add(vertex);
                                        }
                                    }
                                }
                                blendShapesList.Add(blendShape);
                            }
                            vlskinningRenderer.BlendShapesList = blendShapesList;
                        }
                        tmp1 = obj["blendShapeWeights"] as OrderedDictionary;
                        if (tmp1 != null)
                        {
                            var blendShapeWeightsList = new List<float>();

                            foreach (DictionaryEntry entry in tmp1)
                            {

                                if (entry.Value is OrderedDictionary weightData)
                                {
                                    for (int i = 0; i <= 15; i++)
                                    {
                                        var key = "w" + i;
                                        if (weightData.Contains(key))
                                        {
                                            try
                                            {
                                                blendShapeWeightsList.Add(Convert.ToSingle(weightData[key]));
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Error converting value for key {key}: {ex.Message}");
                                            }
                                        }
                                    }
                                }

                            }
                            vlskinningRenderer.blendShapeWeightsList = blendShapeWeightsList;

                        }
                        tmp1 = obj["mesh"] as OrderedDictionary;
                        if (tmp1 != null)
                        {

                            var m_FileID = Convert.ToInt32(tmp1["m_FileID"]);
                            var m_PathID = Convert.ToInt64(tmp1["m_PathID"]);
                            var mptr = new PPtr<Mesh>(m_FileID, m_PathID, m_GameObject.assetsFile);
                            if (mptr != null)
                            {
                                ptrMesh = mptr;
                            }
                        }

                    }

                    if (m_GameObject.Name == "VLSkinningRenderer")
                    {
                        if (ptrMesh != null)
                        {
                            m_GameObject.m_MeshFilter.m_Mesh = ptrMesh;
                        }
                    }
                    else
                    {
                        var origin = vlskin.reader.Position;
                        var vlRenderer = new SkinnedMeshRenderer(vlskin.m_MeshRenderer.reader);
                        vlskin.reader.Position = origin;
                        vlRenderer.m_AABB = vlskinningRenderer.LocalBounds;
                        vlRenderer.m_Bones = vlskinningRenderer.Bones;
                        vlRenderer.m_BlendShapeWeights = vlskinningRenderer.blendShapeWeightsList.ToArray();
                        vlRenderer.m_Mesh = vlskin.m_MeshFilter.m_Mesh;
                        m_GameObject.m_SkinnedMeshRenderer = vlRenderer;
                        m_GameObject.m_MeshFilter = vlskin.m_MeshFilter;
                        m_GameObject.m_SkinnedMeshRenderer.m_Mesh = ptrMesh;
                        vlskin.m_MeshFilter.m_Mesh = ptrMesh;
                        var mesh = GetMesh(m_GameObject.m_SkinnedMeshRenderer);
                        mesh.m_BindPose = vlskinningRenderer.Bindposes.ToArray();
                        m_GameObject.m_MeshRenderer = vlskin.m_MeshRenderer;
                        m_GameObject.m_MeshFilter = vlskin.m_MeshFilter;
                        var meshBlendShapeList = new List<MeshBlendShape>();
                        var blendShapeVertexList = new List<BlendShapeVertex>();
                        var shapeChanneList = new List<MeshBlendShapeChannel>();
                        var index = 0;
                        var firstVertex = 0;
                        foreach (var shape in vlskinningRenderer.BlendShapesList)
                        {
                            var blendShape = new MeshBlendShape
                            {
                                name = shape.BlendShapeName,
                                firstVertex = (uint)firstVertex,
                                vertexCount = (uint)shape.Vertices.Count,
                                hasNormals = false,
                                hasTangents = false
                            };
                            var shapeChannel = new MeshBlendShapeChannel
                            {
                                name = shape.BlendShapeName,
                                nameHash = System.IO.Hashing.Crc32.HashToUInt32(Encoding.ASCII.GetBytes(shape.BlendShapeName)),
                                frameIndex = index,
                                frameCount = 1,
                            };
                            index++;
                            meshBlendShapeList.Add(blendShape);
                            shapeChanneList.Add(shapeChannel);
                            firstVertex += shape.Vertices.Count;

                        }
                        foreach (var shape in vlskinningRenderer.BlendShapesList)
                        {
                            foreach (var vertice in shape.Vertices)
                            {
                                var blendShapeVertex = new BlendShapeVertex
                                {
                                    index = (uint)vertice.VertIndex,
                                    vertex = vertice.Position,
                                    normal = Vector3.Zero,
                                    tangent = Vector3.Zero,
                                };
                                blendShapeVertexList.Add(blendShapeVertex);

                            }
                        }
                        mesh.m_Shapes.shapes = meshBlendShapeList;
                        mesh.m_Shapes.vertices = blendShapeVertexList;
                        mesh.m_Shapes.channels = shapeChanneList;
                        mesh.m_Shapes.fullWeights = vlskinningRenderer.blendShapeWeightsList.ToArray();
                    }
                }

            }
            if (m_GameObject.m_MeshRenderer != null)
            {
                ConvertMeshRenderer(m_GameObject.m_MeshRenderer);
            }

            if (m_GameObject.m_SkinnedMeshRenderer != null)
            {
                ConvertMeshRenderer(m_GameObject.m_SkinnedMeshRenderer);
            }

            if (m_GameObject.m_Animation != null)
            {
                foreach (var animation in m_GameObject.m_Animation.m_Animations)
                {
                    if (animation.TryGet(out var animationClip))
                    {
                        if (!boundAnimationPathDic.ContainsKey(animationClip))
                        {
                            boundAnimationPathDic.Add(animationClip, GetTransformPath(m_Transform));
                        }
                        animationClipHashSet.Add(animationClip);
                    }
                }
            }

            foreach (var pptr in m_Transform.m_Children)
            {
                if (pptr.TryGet(out var child))
                    ConvertMeshRenderer(child);
            }
        }

        private void CollectAnimationClip(Animator m_Animator)
        {
            if (m_Animator.m_Controller.TryGet(out var m_Controller))
            {
                switch (m_Controller)
                {
                    case AnimatorOverrideController m_AnimatorOverrideController:
                        {
                            if (m_AnimatorOverrideController.m_Controller.TryGet<AnimatorController>(out var m_AnimatorController))
                            {
                                foreach (var pptr in m_AnimatorController.m_AnimationClips)
                                {
                                    if (pptr.TryGet(out var m_AnimationClip))
                                    {
                                        animationClipHashSet.Add(m_AnimationClip);
                                    }
                                }
                            }
                            break;
                        }

                    case AnimatorController m_AnimatorController:
                        {
                            foreach (var pptr in m_AnimatorController.m_AnimationClips)
                            {
                                if (pptr.TryGet(out var m_AnimationClip))
                                {
                                    animationClipHashSet.Add(m_AnimationClip);
                                }
                            }
                            break;
                        }
                }
            }
        }

        private ImportedFrame ConvertTransform(Transform trans)
        {
            var frame = new ImportedFrame(trans.m_Children.Count);
            transformDictionary.Add(trans, frame);
            trans.m_GameObject.TryGet(out var m_GameObject);
            frame.Name = m_GameObject.m_Name;
            SetFrame(frame, trans.m_LocalPosition, trans.m_LocalRotation, trans.m_LocalScale);
            return frame;
        }

        private static ImportedFrame CreateFrame(string name, Vector3 t, Quaternion q, Vector3 s)
        {
            var frame = new ImportedFrame();
            frame.Name = name;
            SetFrame(frame, t, q, s);
            return frame;
        }

        private static void SetFrame(ImportedFrame frame, Vector3 t, Quaternion q, Vector3 s)
        {
            frame.LocalPosition = new Vector3(-t.X, t.Y, t.Z);
            frame.LocalRotation = new Quaternion(q.X, -q.Y, -q.Z, q.W);
            frame.LocalScale = s;
        }

        private void ConvertTransforms(Transform trans, ImportedFrame parent)
        {
            var frame = ConvertTransform(trans);
            if (parent == null)
            {
                RootFrame = frame;
            }
            else
            {
                parent.AddChild(frame);
            }
            foreach (var pptr in trans.m_Children)
            {
                if (pptr.TryGet(out var child))
                    ConvertTransforms(child, frame);
            }
        }

        private void ConvertMeshRenderer(Renderer meshR)
        {
            var mesh = GetMesh(meshR);
            if (mesh == null)
                return;
            var iMesh = new ImportedMesh();
            meshR.m_GameObject.TryGet(out var m_GameObject2);
            iMesh.Path = GetTransformPath(m_GameObject2.m_Transform);
            iMesh.SubmeshList = new List<ImportedSubmesh>();
            var subHashSet = new HashSet<int>();
            var combine = false;
            int firstSubMesh = 0;
            if (meshR.m_StaticBatchInfo?.subMeshCount > 0)
            {
                firstSubMesh = meshR.m_StaticBatchInfo.firstSubMesh;
                var finalSubMesh = meshR.m_StaticBatchInfo.firstSubMesh + meshR.m_StaticBatchInfo.subMeshCount;
                for (int i = meshR.m_StaticBatchInfo.firstSubMesh; i < finalSubMesh; i++)
                {
                    subHashSet.Add(i);
                }
                combine = true;
            }
            else if (meshR.m_SubsetIndices?.Length > 0)
            {
                firstSubMesh = (int)meshR.m_SubsetIndices.Min(x => x);
                foreach (var index in meshR.m_SubsetIndices)
                {
                    subHashSet.Add((int)index);
                }
                combine = true;
            }

            iMesh.hasNormal = mesh.m_Normals?.Length > 0;
            iMesh.hasUV = new bool[8];
            iMesh.uvType = new int[8];
            for (int uv = 0; uv < 8; uv++)
            {
                var key = $"UV{uv}";
                iMesh.hasUV[uv] = mesh.GetUV(uv)?.Length > 0 && options.uvs[key].Item1;
                iMesh.uvType[uv] = options.uvs[key].Item2;
            }
            iMesh.hasTangent = mesh.m_Tangents != null && mesh.m_Tangents.Length == mesh.m_VertexCount * 4;
            iMesh.hasColor = mesh.m_Colors?.Length > 0;

            int firstFace = 0;
            for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
            {
                int numFaces = (int)mesh.m_SubMeshes[i].indexCount / 3;
                if (subHashSet.Count > 0 && !subHashSet.Contains(i))
                {
                    firstFace += numFaces;
                    continue;
                }
                var submesh = mesh.m_SubMeshes[i];
                var iSubmesh = new ImportedSubmesh();
                Material mat = null;
                if (i - firstSubMesh < meshR.m_Materials.Count)
                {
                    if (meshR.m_Materials[i - firstSubMesh].TryGet(out var m_Material))
                    {
                        mat = m_Material;
                    }
                }
                ImportedMaterial iMat = ConvertMaterial(mat);
                iSubmesh.Material = iMat.Name;
                iSubmesh.BaseVertex = (int)mesh.m_SubMeshes[i].firstVertex;

                //Face
                iSubmesh.FaceList = new List<ImportedFace>(numFaces);
                var end = firstFace + numFaces;
                for (int f = firstFace; f < end; f++)
                {
                    var face = new ImportedFace();
                    face.VertexIndices = new int[3];
                    face.VertexIndices[0] = (int)(mesh.m_Indices[f * 3 + 2] - submesh.firstVertex);
                    face.VertexIndices[1] = (int)(mesh.m_Indices[f * 3 + 1] - submesh.firstVertex);
                    face.VertexIndices[2] = (int)(mesh.m_Indices[f * 3] - submesh.firstVertex);
                    iSubmesh.FaceList.Add(face);
                }
                firstFace = end;

                iMesh.SubmeshList.Add(iSubmesh);
            }

            // Shared vertex list
            iMesh.VertexList = new List<ImportedVertex>((int)mesh.m_VertexCount);
            for (var j = 0; j < mesh.m_VertexCount; j++)
            {
                var iVertex = new ImportedVertex();
                //Vertices
                int c = 3;
                if (mesh.m_Vertices.Length == mesh.m_VertexCount * 4)
                {
                    c = 4;
                }
                iVertex.Vertex = new Vector3(-mesh.m_Vertices[j * c], mesh.m_Vertices[j * c + 1], mesh.m_Vertices[j * c + 2]);
                //Normals
                if (iMesh.hasNormal)
                {
                    if (mesh.m_Normals.Length == mesh.m_VertexCount * 3)
                    {
                        c = 3;
                    }
                    else if (mesh.m_Normals.Length == mesh.m_VertexCount * 4)
                    {
                        c = 4;
                    }
                    iVertex.Normal = new Vector3(-mesh.m_Normals[j * c], mesh.m_Normals[j * c + 1], mesh.m_Normals[j * c + 2]);
                }
                //UV
                iVertex.UV = new float[8][];
                for (int uv = 0; uv < 8; uv++)
                {
                    if (iMesh.hasUV[uv])
                    {
                        c = 4;
                        var m_UV = mesh.GetUV(uv);
                        if (m_UV.Length == mesh.m_VertexCount * 2)
                        {
                            c = 2;
                        }
                        else if (m_UV.Length == mesh.m_VertexCount * 3)
                        {
                            c = 3;
                        }
                        iVertex.UV[uv] = new[] { m_UV[j * c], m_UV[j * c + 1] };
                    }
                }
                //Tangent
                if (iMesh.hasTangent)
                {
                    iVertex.Tangent = new Vector4(-mesh.m_Tangents[j * 4], mesh.m_Tangents[j * 4 + 1], mesh.m_Tangents[j * 4 + 2], mesh.m_Tangents[j * 4 + 3]);
                }
                //Colors
                if (iMesh.hasColor)
                {
                    if (mesh.m_Colors.Length == mesh.m_VertexCount * 3)
                    {
                        iVertex.Color = new Color(mesh.m_Colors[j * 3], mesh.m_Colors[j * 3 + 1], mesh.m_Colors[j * 3 + 2], 1.0f);
                    }
                    else
                    {
                        iVertex.Color = new Color(mesh.m_Colors[j * 4], mesh.m_Colors[j * 4 + 1], mesh.m_Colors[j * 4 + 2], mesh.m_Colors[j * 4 + 3]);
                    }
                }
                //BoneInfluence
                if (mesh.m_Skin?.Count > 0)
                {
                    var inf = mesh.m_Skin[j];
                    iVertex.BoneIndices = new int[4];
                    iVertex.Weights = new float[4];
                    for (var k = 0; k < 4; k++)
                    {
                        iVertex.BoneIndices[k] = inf.boneIndex[k];
                        iVertex.Weights[k] = inf.weight[k];
                    }
                }
                iMesh.VertexList.Add(iVertex);
            }

            if (meshR is SkinnedMeshRenderer sMesh)
            {
                meshR.m_GameObject.TryGet(out GameObject test);
                var NN4GO = test.m_Components.Find(x => x.Name == "NN4SkinnedMeshRendererData");
                if (NN4GO != null)
                {
                  
                    if (NN4GO.TryGet(out MonoBehaviour NN4SkinnedMeshRendererData))
                    { 
                        Console.WriteLine(NN4SkinnedMeshRendererData.m_PathID);
                        var obj = NN4SkinnedMeshRendererData.ToType();
                        var bones = obj["Bones"] as List<object>;
                        var transforms = meshR.assetsFile.ObjectsDic
                            .Where(kvp => kvp.Value.type == ClassIDType.Transform);
                            for (int i = 0; i < bones.Count; i++)
                        {   var NN4Bone = (string)bones[i];
                            var transform = transforms
                            .Select(kvp => kvp.Value as AssetStudio.Transform) 
                            .FirstOrDefault(obj => obj.m_GameObject.Name.Contains(NN4Bone));
                            if (transform != null)
                            {
                                Logger.Debug($"Found transform with name: {NN4Bone}");
                                sMesh.m_Bones[i].Set(transform);
                            }

                        }

                    }               
                }
                    //Bone
                    /*
                     * 0 - None
                     * 1 - m_Bones
                     * 2 - m_BoneNameHashes
                     */
                    var boneType = 0;
                if (sMesh.m_Bones.Count > 0)
                {
                    if (sMesh.m_Bones.Count == mesh.m_BindPose.Length)
                    {
                        var verifiedBoneCount = sMesh.m_Bones.Count(x => x.TryGet(out _));
                        if (verifiedBoneCount > 0)
                        {
                            boneType = 1;
                        }
                        if (verifiedBoneCount != sMesh.m_Bones.Count)
                        {
                            //尝试使用m_BoneNameHashes 4.3 and up
                            if (mesh.m_BindPose.Length > 0 && (mesh.m_BindPose.Length == mesh.m_BoneNameHashes?.Length))
                            {
                                //有效bone数量是否大于SkinnedMeshRenderer
                                var verifiedBoneCount2 = mesh.m_BoneNameHashes.Count(x => FixBonePath(GetPathFromHash(x)) != null);
                                if (verifiedBoneCount2 > verifiedBoneCount)
                                {
                                    boneType = 2;
                                }
                            }
                        }
                    }
                }
                if (boneType == 0)
                {
                    //尝试使用m_BoneNameHashes 4.3 and up
                    if (mesh.m_BindPose.Length > 0 && (mesh.m_BindPose.Length == mesh.m_BoneNameHashes?.Length))
                    {
                        var verifiedBoneCount = mesh.m_BoneNameHashes.Count(x => FixBonePath(GetPathFromHash(x)) != null);
                        if (verifiedBoneCount > 0)
                        {
                            boneType = 2;
                        }
                    }
                }

                if (boneType == 1)
                {
                    var boneCount = sMesh.m_Bones.Count;
                    iMesh.BoneList = new List<ImportedBone>(boneCount);
                    for (int i = 0; i < boneCount; i++)
                    {
                        var bone = new ImportedBone();
                        if (sMesh.m_Bones[i].TryGet(out var m_Transform))
                        {
                            bone.Path = GetTransformPath(m_Transform);
                        }
                        var convert = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                        bone.Matrix = convert * mesh.m_BindPose[i] * convert;
                        iMesh.BoneList.Add(bone);
                    }
                }
                else if (boneType == 2)
                {
                    var boneCount = mesh.m_BindPose.Length;
                    iMesh.BoneList = new List<ImportedBone>(boneCount);
                    for (int i = 0; i < boneCount; i++)
                    {
                        var bone = new ImportedBone();
                        var boneHash = mesh.m_BoneNameHashes[i];
                        var path = GetPathFromHash(boneHash);
                        bone.Path = FixBonePath(path);
                        var convert = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                        bone.Matrix = convert * mesh.m_BindPose[i] * convert;
                        iMesh.BoneList.Add(bone);
                    }
                }

                //Morphs
                if (mesh.m_Shapes?.channels?.Count > 0)
                {
                    var morph = new ImportedMorph();
                    MorphList.Add(morph);
                    morph.Path = iMesh.Path;
                    morph.Channels = new List<ImportedMorphChannel>(mesh.m_Shapes.channels.Count);
                    for (int i = 0; i < mesh.m_Shapes.channels.Count; i++)
                    {
                        var channel = new ImportedMorphChannel();
                        morph.Channels.Add(channel);
                        var shapeChannel = mesh.m_Shapes.channels[i];

                        var blendShapeName = "blendShape." + shapeChannel.name;
                        var crc = new SevenZip.CRC();
                        var bytes = Encoding.UTF8.GetBytes(blendShapeName);
                        crc.Update(bytes, 0, (uint)bytes.Length);
                        morphChannelNames[crc.GetDigest()] = blendShapeName;

                        morphChannelNames[shapeChannel.nameHash] = shapeChannel.name;

                        channel.Name = shapeChannel.name.Split('.').Last();
                        channel.KeyframeList = new List<ImportedMorphKeyframe>(shapeChannel.frameCount);
                        var frameEnd = shapeChannel.frameIndex + shapeChannel.frameCount;
                        for (int frameIdx = shapeChannel.frameIndex; frameIdx < frameEnd; frameIdx++)
                        {
                            var keyframe = new ImportedMorphKeyframe();
                            channel.KeyframeList.Add(keyframe);
                            keyframe.Weight = mesh.m_Shapes.fullWeights[frameIdx];
                            var shape = mesh.m_Shapes.shapes[frameIdx];
                            keyframe.hasNormals = shape.hasNormals;
                            keyframe.hasTangents = shape.hasTangents;
                            keyframe.VertexList = new List<ImportedMorphVertex>((int)shape.vertexCount);
                            var vertexEnd = shape.firstVertex + shape.vertexCount;
                            for (int j = (int)shape.firstVertex; j < vertexEnd; j++)
                            {
                                var destVertex = new ImportedMorphVertex();
                                keyframe.VertexList.Add(destVertex);
                                var morphVertex = mesh.m_Shapes.vertices[j];
                                destVertex.Index = morphVertex.index;
                                var sourceVertex = iMesh.VertexList[(int)morphVertex.index];
                                destVertex.Vertex = new ImportedVertex();
                                var morphPos = morphVertex.vertex;
                                destVertex.Vertex.Vertex = sourceVertex.Vertex + new Vector3(-morphPos.X, morphPos.Y, morphPos.Z);
                                if (shape.hasNormals)
                                {
                                    var morphNormal = morphVertex.normal;
                                    destVertex.Vertex.Normal = new Vector3(-morphNormal.X, morphNormal.Y, morphNormal.Z);
                                }
                                if (shape.hasTangents)
                                {
                                    var morphTangent = morphVertex.tangent;
                                    destVertex.Vertex.Tangent = new Vector4(-morphTangent.X, morphTangent.Y, morphTangent.Z, 0);
                                }
                            }
                        }
                    }
                }
            }

            //TODO combine mesh
            if (combine)
            {
                meshR.m_GameObject.TryGet(out var m_GameObject);
                var frame = RootFrame.FindChild(m_GameObject.m_Name);
                if (frame != null)
                {
                    frame.LocalPosition = RootFrame.LocalPosition;
                    frame.LocalRotation = RootFrame.LocalRotation;
                    while (frame.Parent != null)
                    {
                        frame = frame.Parent;
                        frame.LocalPosition = RootFrame.LocalPosition;
                        frame.LocalRotation = RootFrame.LocalRotation;
                    }
                }
            }

            MeshList.Add(iMesh);
        }

        private static Mesh GetMesh(Renderer meshR)
        {
            if (meshR is SkinnedMeshRenderer sMesh)
            {
                if (sMesh.m_Mesh.TryGet(out var m_Mesh))
                {
                    return m_Mesh;
                }
            }
            else
            {
                meshR.m_GameObject.TryGet(out var m_GameObject);
                if (m_GameObject.m_MeshFilter != null)
                {
                    if (m_GameObject.m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                    {
                        return m_Mesh;
                    }
                }
            }

            return null;
        }

        private string GetTransformPath(Transform transform)
        {
            if (transformDictionary.TryGetValue(transform, out var frame))
            {
                return frame.Path;
            }
            return null;
        }

        private string FixBonePath(AnimationClip m_AnimationClip, string path)
        {
            if (boundAnimationPathDic.TryGetValue(m_AnimationClip, out var basePath))
            {
                path = basePath + "/" + path;
            }
            return FixBonePath(path);
        }

        private string FixBonePath(string path)
        {
            var frame = RootFrame.FindFrameByPath(path);
            return frame?.Path;
        }

        private static string GetTransformPathByFather(Transform transform)
        {
            transform.m_GameObject.TryGet(out var m_GameObject);
            if (transform.m_Father.TryGet(out var father))
            {
                return GetTransformPathByFather(father) + "/" + m_GameObject.m_Name;
            }

            return m_GameObject.m_Name;
        }

        private ImportedMaterial ConvertMaterial(Material mat)
        {
            ImportedMaterial iMat;
            if (mat != null)
            {
                if (options.exportMaterials)
                {
                    options.materials.Add(mat);
                }
                iMat = ImportedHelpers.FindMaterial(mat.m_Name, MaterialList);
                if (iMat != null)
                {
                    return iMat;
                }
                iMat = new ImportedMaterial();
                iMat.Name = mat.m_Name;
                //default values
                iMat.Diffuse = new Color(0.8f, 0.8f, 0.8f, 1);
                iMat.Ambient = new Color(0.2f, 0.2f, 0.2f, 1);
                iMat.Emissive = new Color(0, 0, 0, 1);
                iMat.Specular = new Color(0.2f, 0.2f, 0.2f, 1);
                iMat.Reflection = new Color(0, 0, 0, 1);
                iMat.Shininess = 20f;
                iMat.Transparency = 0f;
                foreach (var col in mat.m_SavedProperties.m_Colors)
                {
                    switch (col.Key)
                    {
                        case "_Color":
                            iMat.Diffuse = col.Value;
                            break;
                        case "_SColor":
                            iMat.Ambient = col.Value;
                            break;
                        case "_EmissionColor":
                            iMat.Emissive = col.Value;
                            break;
                        case "_SpecularColor":
                            iMat.Specular = col.Value;
                            break;
                        case "_ReflectColor":
                            iMat.Reflection = col.Value;
                            break;
                    }
                }

                foreach (var flt in mat.m_SavedProperties.m_Floats)
                {
                    switch (flt.Key)
                    {
                        case "_Shininess":
                            iMat.Shininess = flt.Value;
                            break;
                        case "_Transparency":
                            iMat.Transparency = flt.Value;
                            break;
                    }
                }

                //textures
                iMat.Textures = new List<ImportedMaterialTexture>();
                foreach (var texEnv in mat.m_SavedProperties.m_TexEnvs)
                {
                    if (!texEnv.Value.m_Texture.TryGet<Texture2D>(out var m_Texture2D)) //TODO other Texture
                    {
                        continue;
                    }

                    var texture = new ImportedMaterialTexture();
                    iMat.Textures.Add(texture);

                    int dest = -1;
                    if (options.texs.TryGetValue(texEnv.Key, out var target))
                        dest = target;
                    else if (texEnv.Key == "_MainTex")
                        dest = 0;
                    else if (texEnv.Key == "_BumpMap")
                        dest = 3;
                    else if (texEnv.Key.Contains("Specular"))
                        dest = 2;
                    else if (texEnv.Key.Contains("Normal"))
                        dest = 1;

                    texture.Dest = dest;

                    var ext = $".{options.imageFormat.ToString().ToLower()}";
                    if (textureNameDictionary.TryGetValue(m_Texture2D, out var textureName))
                    {
                        texture.Name = textureName;
                    }
                    else if (ImportedHelpers.FindTexture(m_Texture2D.m_Name + ext, TextureList) != null) //已有相同名字的图片
                    {
                        for (int i = 1; ; i++)
                        {
                            var name = m_Texture2D.m_Name + $" ({i}){ext}";
                            if (ImportedHelpers.FindTexture(name, TextureList) == null)
                            {
                                texture.Name = name;
                                textureNameDictionary.Add(m_Texture2D, name);
                                break;
                            }
                        }
                    }
                    else
                    {
                        texture.Name = m_Texture2D.m_Name + ext;
                        textureNameDictionary.Add(m_Texture2D, texture.Name);
                    }

                    texture.Offset = texEnv.Value.m_Offset;
                    texture.Scale = texEnv.Value.m_Scale;
                    ConvertTexture2D(m_Texture2D, texture.Name);
                }

                MaterialList.Add(iMat);
            }
            else
            {
                iMat = new ImportedMaterial();
            }
            return iMat;
        }

        private void ConvertTexture2D(Texture2D m_Texture2D, string name)
        {
            var iTex = ImportedHelpers.FindTexture(name, TextureList);
            if (iTex != null)
            {
                return;
            }

            var stream = m_Texture2D.ConvertToStream(options.imageFormat, true);
            if (stream != null)
            {
                using (stream)
                {
                    iTex = new ImportedTexture(stream, name);
                    TextureList.Add(iTex);
                }
            }
        }

        private void ConvertAnimations()
        {
            foreach (var animationClip in animationClipHashSet)
            {
                var iAnim = new ImportedKeyframedAnimation();
                var name = animationClip.m_Name;
                if (AnimationList.Exists(x => x.Name == name))
                {
                    for (int i = 1; ; i++)
                    {
                        var fixName = name + $"_{i}";
                        if (!AnimationList.Exists(x => x.Name == fixName))
                        {
                            name = fixName;
                            break;
                        }
                    }
                }
                iAnim.Name = name;
                iAnim.SampleRate = animationClip.m_SampleRate;
                iAnim.TrackList = new List<ImportedAnimationKeyframedTrack>();
                AnimationList.Add(iAnim);
                if (animationClip.m_Legacy)
                {
                    foreach (var m_CompressedRotationCurve in animationClip.m_CompressedRotationCurves)
                    {
                        var track = iAnim.FindTrack(FixBonePath(animationClip, m_CompressedRotationCurve.m_Path));

                        var numKeys = m_CompressedRotationCurve.m_Times.m_NumItems;
                        var data = m_CompressedRotationCurve.m_Times.UnpackInts();
                        var times = new float[numKeys];
                        int t = 0;
                        for (int i = 0; i < numKeys; i++)
                        {
                            t += data[i];
                            times[i] = t * 0.01f;
                        }
                        var quats = m_CompressedRotationCurve.m_Values.UnpackQuats();

                        for (int i = 0; i < numKeys; i++)
                        {
                            var quat = quats[i];
                            var value = new Quaternion(quat.X, -quat.Y, -quat.Z, quat.W);
                            track.Rotations.Add(new ImportedKeyframe<Quaternion>(times[i], value));
                        }
                    }
                    foreach (var m_RotationCurve in animationClip.m_RotationCurves)
                    {
                        var track = iAnim.FindTrack(FixBonePath(animationClip, m_RotationCurve.path));
                        foreach (var m_Curve in m_RotationCurve.curve.m_Curve)
                        {
                            var value = new Quaternion(m_Curve.value.X, -m_Curve.value.Y, -m_Curve.value.Z, m_Curve.value.W);
                            track.Rotations.Add(new ImportedKeyframe<Quaternion>(m_Curve.time, value));
                        }
                    }
                    foreach (var m_PositionCurve in animationClip.m_PositionCurves)
                    {
                        var track = iAnim.FindTrack(FixBonePath(animationClip, m_PositionCurve.path));
                        foreach (var m_Curve in m_PositionCurve.curve.m_Curve)
                        {
                            track.Translations.Add(new ImportedKeyframe<Vector3>(m_Curve.time, new Vector3(-m_Curve.value.X, m_Curve.value.Y, m_Curve.value.Z)));
                        }
                    }
                    foreach (var m_ScaleCurve in animationClip.m_ScaleCurves)
                    {
                        var track = iAnim.FindTrack(FixBonePath(animationClip, m_ScaleCurve.path));
                        foreach (var m_Curve in m_ScaleCurve.curve.m_Curve)
                        {
                            track.Scalings.Add(new ImportedKeyframe<Vector3>(m_Curve.time, new Vector3(m_Curve.value.X, m_Curve.value.Y, m_Curve.value.Z)));
                        }
                    }
                    if (animationClip.m_EulerCurves != null)
                    {
                        foreach (var m_EulerCurve in animationClip.m_EulerCurves)
                        {
                            var track = iAnim.FindTrack(FixBonePath(animationClip, m_EulerCurve.path));
                            foreach (var m_Curve in m_EulerCurve.curve.m_Curve)
                            {
                                var value = Fbx.EulerToQuaternion(new Vector3(m_Curve.value.X, -m_Curve.value.Y, -m_Curve.value.Z));
                                track.Rotations.Add(new ImportedKeyframe<Quaternion>(m_Curve.time, value));
                            }
                        }
                    }
                    foreach (var m_FloatCurve in animationClip.m_FloatCurves)
                    {
                        if (m_FloatCurve.classID == ClassIDType.SkinnedMeshRenderer) //BlendShape
                        {
                            var channelName = m_FloatCurve.attribute;
                            int dotPos = channelName.IndexOf('.');
                            if (dotPos >= 0)
                            {
                                channelName = channelName.Substring(dotPos + 1);
                            }

                            var path = GetPathByChannelName(channelName);
                            if (string.IsNullOrEmpty(path))
                            {
                                path = FixBonePath(animationClip, m_FloatCurve.path);
                            }
                            var track = iAnim.FindTrack(path, channelName);
                            if (track.BlendShape == null)
                            {
                                track.BlendShape = new ImportedBlendShape();
                                track.BlendShape.ChannelName = channelName;
                            }
                            foreach (var m_Curve in m_FloatCurve.curve.m_Curve)
                            {
                                track.BlendShape.Keyframes.Add(new ImportedKeyframe<float>(m_Curve.time, m_Curve.value));
                            }
                        }
                    }
                }
                else
                {
                    var m_Clip = animationClip.m_MuscleClip.m_Clip;
                    var streamedFrames = m_Clip.m_StreamedClip.ReadData();
                    var m_ClipBindingConstant = animationClip.m_ClipBindingConstant ?? m_Clip.ConvertValueArrayToGenericBinding();
                    var m_ACLClip = m_Clip.m_ACLClip;
                    var aclCount = m_ACLClip.CurveCount;
                    if (m_ACLClip.IsSet && !options.game.Type.IsSRGroup())
                    {
                        m_ACLClip.Process(options.game, out var values, out var times);
                        for (int frameIndex = 0; frameIndex < times.Length; frameIndex++)
                        {
                            var time = times[frameIndex];
                            var frameOffset = frameIndex * m_ACLClip.CurveCount;
                            for (int curveIndex = 0; curveIndex < m_ACLClip.CurveCount;)
                            {
                                var index = curveIndex;
                                ReadCurveData(iAnim, m_ClipBindingConstant, index, time, values, (int)frameOffset, ref curveIndex);
                            }

                        }
                    }
                    for (int frameIndex = 1; frameIndex < streamedFrames.Count - 1; frameIndex++)
                    {
                        var frame = streamedFrames[frameIndex];
                        var streamedValues = frame.keyList.Select(x => x.value).ToArray();
                        for (int curveIndex = 0; curveIndex < frame.keyList.Count;)
                        {
                            var index = frame.keyList[curveIndex].index;
                            if (!options.game.Type.IsSRGroup())
                                index += (int)aclCount;
                            ReadCurveData(iAnim, m_ClipBindingConstant, index, frame.time, streamedValues, 0, ref curveIndex);
                        }
                    }
                    var m_DenseClip = m_Clip.m_DenseClip;
                    var streamCount = m_Clip.m_StreamedClip.curveCount;
                    for (int frameIndex = 0; frameIndex < m_DenseClip.m_FrameCount; frameIndex++)
                    {
                        var time = m_DenseClip.m_BeginTime + frameIndex / m_DenseClip.m_SampleRate;
                        var frameOffset = frameIndex * m_DenseClip.m_CurveCount;
                        for (int curveIndex = 0; curveIndex < m_DenseClip.m_CurveCount;)
                        {
                            var index = streamCount + curveIndex;
                            if (!options.game.Type.IsSRGroup())
                                index += (int)aclCount;
                            ReadCurveData(iAnim, m_ClipBindingConstant, (int)index, time, m_DenseClip.m_SampleArray, (int)frameOffset, ref curveIndex);
                        }
                    }
                    if (m_ACLClip.IsSet && options.game.Type.IsSRGroup())
                    {
                        m_ACLClip.Process(options.game, out var values, out var times);
                        for (int frameIndex = 0; frameIndex < times.Length; frameIndex++)
                        {
                            var time = times[frameIndex];
                            var frameOffset = frameIndex * m_ACLClip.CurveCount;
                            for (int curveIndex = 0; curveIndex < m_ACLClip.CurveCount;)
                            {
                                var index = (int)(curveIndex + m_DenseClip.m_CurveCount + streamCount);
                                ReadCurveData(iAnim, m_ClipBindingConstant, index, time, values, (int)frameOffset, ref curveIndex);
                            }

                        }
                    }
                    if (m_Clip.m_ConstantClip != null)
                    {
                        var m_ConstantClip = m_Clip.m_ConstantClip;
                        var denseCount = m_Clip.m_DenseClip.m_CurveCount;
                        var time2 = 0.0f;
                        for (int i = 0; i < 2; i++)
                        {
                            for (int curveIndex = 0; curveIndex < m_ConstantClip.data.Length;)
                            {
                                var index = aclCount + streamCount + denseCount + curveIndex;
                                ReadCurveData(iAnim, m_ClipBindingConstant, (int)index, time2, m_ConstantClip.data, 0, ref curveIndex);
                            }
                            time2 = animationClip.m_MuscleClip.m_StopTime;
                        }
                    }
                }
            }
        }

        private void ReadCurveData(ImportedKeyframedAnimation iAnim, AnimationClipBindingConstant m_ClipBindingConstant, int index, float time, float[] data, int offset, ref int curveIndex)
        {
            var binding = m_ClipBindingConstant.FindBinding(index);
            if (binding.typeID == ClassIDType.SkinnedMeshRenderer) //BlendShape
            {
                var channelName = GetChannelNameFromHash(binding.attribute);
                if (string.IsNullOrEmpty(channelName))
                {
                    curveIndex++;
                    return;
                }
                int dotPos = channelName.IndexOf('.');
                if (dotPos >= 0)
                {
                    channelName = channelName.Substring(dotPos + 1);
                }

                var path = GetPathByChannelName(channelName);
                if (string.IsNullOrEmpty(path))
                {
                    path = FixBonePath(GetPathFromHash(binding.path));
                }
                var track = iAnim.FindTrack(path, channelName);
                if (track.BlendShape == null)
                {
                    track.BlendShape = new ImportedBlendShape();
                    track.BlendShape.ChannelName = channelName;
                }
                track.BlendShape.Keyframes.Add(new ImportedKeyframe<float>(time, data[curveIndex++ + offset]));
            }
            else if (binding.typeID == ClassIDType.Transform)
            {
                var path = FixBonePath(GetPathFromHash(binding.path));
                var track = iAnim.FindTrack(path);

                switch (binding.attribute)
                {
                    case 1:
                        track.Translations.Add(new ImportedKeyframe<Vector3>(time, new Vector3
                        (
                            -data[curveIndex++ + offset],
                            data[curveIndex++ + offset],
                            data[curveIndex++ + offset]
                        )));
                        break;
                    case 2:
                        track.Rotations.Add(new ImportedKeyframe<Quaternion>(time, new Quaternion
                        (
                            data[curveIndex++ + offset],
                            -data[curveIndex++ + offset],
                            -data[curveIndex++ + offset],
                            data[curveIndex++ + offset]
                        )));
                        break;
                    case 3:
                        track.Scalings.Add(new ImportedKeyframe<Vector3>(time, new Vector3
                        (
                            data[curveIndex++ + offset],
                            data[curveIndex++ + offset],
                            data[curveIndex++ + offset]
                        )));
                        break;
                    case 4:
                        var value = Fbx.EulerToQuaternion(new Vector3
                        (
                            data[curveIndex++ + offset],
                            -data[curveIndex++ + offset],
                            -data[curveIndex++ + offset]
                        ));
                        track.Rotations.Add(new ImportedKeyframe<Quaternion>(time, value));
                        break;
                    default:
                        curveIndex++;
                        break;
                }
            }
            else
            {
                curveIndex++;
            }
        }

        private string GetPathFromHash(uint hash)
        {
            bonePathHash.TryGetValue(hash, out var boneName);
            if (string.IsNullOrEmpty(boneName))
            {
                boneName = avatar?.FindBonePath(hash);
            }
            if (string.IsNullOrEmpty(boneName))
            {
                boneName = "unknown " + hash;
            }
            return boneName;
        }

        private void CreateBonePathHash(Transform m_Transform)
        {
            var name = GetTransformPathByFather(m_Transform);
            var crc = new SevenZip.CRC();
            var bytes = Encoding.UTF8.GetBytes(name);
            crc.Update(bytes, 0, (uint)bytes.Length);
            bonePathHash[crc.GetDigest()] = name;
            int index;
            while ((index = name.IndexOf("/", StringComparison.Ordinal)) >= 0)
            {
                name = name.Substring(index + 1);
                crc = new SevenZip.CRC();
                bytes = Encoding.UTF8.GetBytes(name);
                crc.Update(bytes, 0, (uint)bytes.Length);
                bonePathHash[crc.GetDigest()] = name;
            }
            foreach (var pptr in m_Transform.m_Children)
            {
                if (pptr.TryGet(out var child))
                    CreateBonePathHash(child);
            }
        }

        private void DeoptimizeTransformHierarchy()
        {
            if (avatar == null)
                throw new Exception("Transform hierarchy has been optimized, but can't find Avatar to deoptimize.");
            // 1. Figure out the skeletonPaths from the unstripped avatar
            var skeletonPaths = new List<string>();
            foreach (var id in avatar.m_Avatar.m_AvatarSkeleton.m_ID)
            {
                var path = avatar.FindBonePath(id);
                skeletonPaths.Add(path);
            }
            // 2. Restore the original transform hierarchy
            // Prerequisite: skeletonPaths follow pre-order traversal
            for (var i = 1; i < skeletonPaths.Count; i++) // start from 1, skip the root transform because it will always be there.
            {
                var path = skeletonPaths[i];
                var strs = path.Split('/');
                string transformName;
                ImportedFrame parentFrame;
                if (strs.Length == 1)
                {
                    transformName = path;
                    parentFrame = RootFrame;
                }
                else
                {
                    transformName = strs.Last();
                    var parentFramePath = path.Substring(0, path.LastIndexOf('/'));
                    parentFrame = RootFrame.FindRelativeFrameWithPath(parentFramePath);
                }
                var skeletonPose = avatar.m_Avatar.m_DefaultPose;
                var xform = skeletonPose.m_X[i];
                var frame = RootFrame.FindChild(transformName);
                if (frame != null)
                {
                    SetFrame(frame, xform.t, xform.q, xform.s);
                }
                else
                {
                    frame = CreateFrame(transformName, xform.t, xform.q, xform.s);
                }
                parentFrame.AddChild(frame);
            }
        }

        private string GetPathByChannelName(string channelName)
        {
            foreach (var morph in MorphList)
            {
                foreach (var channel in morph.Channels)
                {
                    if (channel.Name == channelName)
                    {
                        return morph.Path;
                    }
                }
            }
            return null;
        }

        private string GetChannelNameFromHash(uint attribute)
        {
            if (morphChannelNames.TryGetValue(attribute, out var name))
            {
                return name;
            }
            else
            {
                return null;
            }
        }

        public record Options
        {
            public ImageFormat imageFormat;
            public Game game;
            public bool collectAnimations;
            public bool exportMaterials;
            public HashSet<Material> materials;
            public Dictionary<string, (bool, int)> uvs;
            public Dictionary<string, int> texs; 
        }
    }
}
