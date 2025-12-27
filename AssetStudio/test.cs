using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static AssetStudio.AssetsHelper;

namespace AssetStudio
{
    public static class AssetsHelperParallel
    {

        public static ConcurrentDictionary<string, Entry> CABMap = new();
        public static ConcurrentBag<AssetEntry> AllAssets = new();
        public static async Task BuildBothParallel(
     string[] files,
     string mapName,
     string baseFolder,
     Game game,
     string savePath,
     ExportListType exportListType,
     ClassIDType[] typeFilters = null,
     Regex[] nameFilters = null,
     Regex[] containerFilters = null)
        {
            Logger.Info("Building Both in Parallel...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            int totalCollisions = 0;
            int totalFiles = files.Length;
            int totalFilesProcessed = 0;
            Logger.Info($"Total files: {totalFiles}");

            var slices = ChunkFilesDynamic(files).ToList();
            Logger.Info($"Total slices: {slices.Count}");

            var bag = new ConcurrentBag<AssetEntry>();
            var sliceStats = new ConcurrentBag<(int fileCount, int assetCount, int collisions)>();

            double lastElapsed = 0;
            AssetsHelper.BaseFolder = baseFolder;
            Parallel.ForEach(
                slices,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                slice =>
                {
                    int localCollisions = 0;
                    var localAssets = new List<AssetEntry>();
                    var manager = new AssetsManager() { Game = game, Silent = true, SkipProcess = true, ResolveDependencies = false, paritial = AssetsHelper.paritial };
                    //var fileStopwatch = Stopwatch.StartNew();
                    var loadedFiles = AssetsHelper.LoadFiles(slice, manager, totalFiles, (processed, total, message) =>
                    {
                        int current = Interlocked.Add(ref totalFilesProcessed, processed);

                        //double totalElapsed = stopwatch.Elapsed.TotalSeconds;


                        //double perFileTime = fileStopwatch.Elapsed.TotalSeconds / processed;

                        Logger.Info($"[{current}/{total}] {message}");
                        //Logger.Info($"[Approx. time per file: {perFileTime:F2}s | Total elapsed: {totalElapsed:F2}s]");


                        //fileStopwatch.Restart();

                    });

                    foreach (var file in loadedFiles)
                    {
                        BuildCABMapSlice(file, manager.assetsFileList, ref localCollisions);
                        BuildAssetMapSlice(file, manager.assetsFileList, manager, localAssets,
                                           typeFilters, nameFilters, containerFilters);
                    }

                    foreach (var asset in localAssets)
                        bag.Add(asset);

                    Interlocked.Add(ref totalCollisions, localCollisions);

                    sliceStats.Add((slice.Length, localAssets.Count, localCollisions));
                    manager.Clear();
                    manager.assetsFileList.Clear();
                    manager.PathIdToPptrs.Clear();
                });

            var finalAssets = bag.ToList();

            Logger.Info("=== Slice Summary ===");
            int sliceIndex = 1;
            foreach (var (fileCount, assetCount, collisions) in sliceStats)
            {
                Logger.Info($"Slice {sliceIndex}: {fileCount} files, {assetCount} assets, {collisions} collisions");
                sliceIndex++;
            }
            AssetsHelper.UpdateContainers(finalAssets, game);
            AssetsHelper.DumpCABMap(mapName, AssetsHelperParallel.CABMap);

            Logger.Info($"Map built successfully! {totalCollisions} collisions found");

            await AssetsHelper.ExportAssetsMap(finalAssets, game, mapName, savePath, exportListType);

            stopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"BuildBothParallel {mapName} completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
            Console.ResetColor();
        }


        public static async Task BuildBothParallelTest(
    string[] files,
    string mapName,
    string baseFolder,
    Game game,
    string savePath,
    ExportListType exportListType,
    ClassIDType[] typeFilters = null,
    Regex[] nameFilters = null,
    Regex[] containerFilters = null)
        {
            Logger.Info("Building Both in Parallel...");
            //const long sixteenGB = 1L * 1024 * 1024 * 1024;

            //bool started = GC.TryStartNoGCRegion(sixteenGB);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            int totalCollisions = 0;
            int totalFiles = files.Length;
            int totalFilesProcessed = 0;
            Logger.Info($"Total files: {totalFiles}");

            var bag = new ConcurrentBag<AssetEntry>();
            var fileStats = new ConcurrentBag<(string file, int assetCount, int collisions)>();
            //var fileTimings = new ConcurrentBag<(string fileName, double elapsedSeconds)>();
            AssetsHelper.BaseFolder = baseFolder;
            double lastElapsed = 0;
            Parallel.ForEach(
                files,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                file =>
                {
                    int localCollisions = 0;
                    var localAssets = new List<AssetEntry>();

                    var manager = new AssetsManager()
                    {
                        Game = game,
                        Silent = true,
                        SkipProcess = true,
                        ResolveDependencies = false,
                        paritial = AssetsHelper.paritial
                    };


                    //var fileStopwatch = Stopwatch.StartNew();

                    var loadedFiles = AssetsHelper.LoadFiles(new[] { file }, manager, totalFiles, (processed, total, message) =>
                    {
                        int current = Interlocked.Add(ref totalFilesProcessed, processed);


                        //double fileTime = fileStopwatch.Elapsed.TotalSeconds;
                        //double totalElapsed = stopwatch.Elapsed.TotalSeconds;

                        Logger.Info($"[{current}/{total}] {message}");
                        //Logger.Info($"[File: {file} | Time: {fileTime:F2}s | Total elapsed: {totalElapsed:F2}s]");


                        //fileTimings.Add((file, fileTime));
                    });

                    foreach (var loaded in loadedFiles)
                    {
                        BuildCABMapSlice(loaded, manager.assetsFileList, ref localCollisions);
                        BuildAssetMapSlice(loaded, manager.assetsFileList, manager, localAssets,
                                           typeFilters, nameFilters, containerFilters);
                    }

                    foreach (var asset in localAssets)
                        bag.Add(asset);

                    Interlocked.Add(ref totalCollisions, localCollisions);
                    fileStats.Add((file, localAssets.Count, localCollisions));

                    manager.Clear();
                    manager.assetsFileList.Clear();
                    manager.PathIdToPptrs.Clear();

                });

            var finalAssets = bag.ToList();


            Logger.Info("=== File Summary ===");
            foreach (var (file, assetCount, collisions) in fileStats)
            {
                Logger.Info($"{Path.GetFileName(file)}: {assetCount} assets, {collisions} collisions");
            }

            AssetsHelper.UpdateContainers(finalAssets, game);
            AssetsHelper.DumpCABMap(mapName);

            Logger.Info($"Map built successfully! {totalCollisions} collisions found");

            await AssetsHelper.ExportAssetsMap(finalAssets, game, mapName, savePath, exportListType);

            stopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"BuildBothParallel {mapName} completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
            //GC.EndNoGCRegion();
            Console.ResetColor();
        }



        private static IEnumerable<string[]> ChunkFilesDynamic(string[] files)
        {
            int totalFiles = files.Length;
            if (totalFiles == 0)
                yield break;


            int sliceCount = Math.Min(totalFiles, Environment.ProcessorCount * 32);
            int sliceSize = (int)Math.Ceiling(totalFiles / (double)sliceCount);

            for (int i = 0; i < totalFiles; i += sliceSize)
            {
                yield return files.Skip(i).Take(sliceSize).ToArray();
            }
        }




        private static void BuildCABMapSlice(string file, List<SerializedFile> assetsFiles, ref int collision)
        {
            var relativePath = Path.GetRelativePath(BaseFolder, file);

            foreach (var assetsFile in assetsFiles)
            {
                var entry = new Entry
                {
                    Path = relativePath,
                    Offset = assetsFile.offset,
                    Dependencies = assetsFile.m_Externals.Select(x => x.fileName).ToList()
                };

                if (!CABMap.TryAdd(assetsFile.fileName, entry))
                {
                    Interlocked.Increment(ref collision);
                }
            }
        }


        private static void BuildAssetMapSlice(
       string file,
       List<SerializedFile> assetsFileList,
       AssetsManager manager,
       List<AssetEntry> assets,
       ClassIDType[] typeFilters = null,
       Regex[] nameFilters = null,
       Regex[] containerFilters = null)

        {
            var matches = new List<AssetEntry>();
            var containers = new List<(PPtr<Object>, string)>();
            var mihoyoBinDataNames = new List<(PPtr<Object>, string)>();
            var objectAssetItemDic = new Dictionary<Object, AssetEntry>();
            var animators = new List<(PPtr<Object>, AssetEntry)>();

            foreach (var assetsFile in assetsFileList)
            {
                foreach (var objInfo in assetsFile.m_Objects)
                {
                    Object obj = null;
                    ObjectReader objectReader = null;

                    try
                    {
                        lock (assetsFile)
                        {
                            objectReader = new ObjectReader(assetsFile.reader, assetsFile, objInfo, manager.Game);

                            obj = new Object(objectReader);
                        }

                        var asset = new AssetEntry
                        {
                            Source = file,
                            PathID = objectReader.m_PathID,
                            Type = objectReader.type,
                            Container = ""
                        };

                        var exportable = false;

                        switch (objectReader.type)
                        {
                            case ClassIDType.AssetBundle when ClassIDType.AssetBundle.CanParse():
                                var assetBundle = new AssetBundle(objectReader);
                                foreach (var m_Container in assetBundle.m_Container)
                                {
                                    var preloadIndex = m_Container.Value.preloadIndex;
                                    var preloadSize = m_Container.Value.preloadSize;
                                    var preloadEnd = preloadIndex + preloadSize;
                                    for (int k = preloadIndex; k < preloadEnd; k++)
                                    {
                                        containers.Add((assetBundle.m_PreloadTable[k], m_Container.Key));
                                    }
                                }

                                obj = null;
                                asset.Name = assetBundle.m_Name;
                                exportable = ClassIDType.AssetBundle.CanExport();
                                break;
                            case ClassIDType.GameObject when ClassIDType.GameObject.CanParse():
                                var gameObject = new GameObject(objectReader);
                                obj = gameObject;
                                asset.Name = gameObject.m_Name;
                                exportable = ClassIDType.GameObject.CanExport();
                                break;
                            case ClassIDType.Shader when ClassIDType.Shader.CanParse():
                                asset.Name = objectReader.ReadAlignedString();
                                if (string.IsNullOrEmpty(asset.Name))
                                {
                                    var m_parsedForm = new SerializedShader(objectReader);
                                    asset.Name = m_parsedForm.m_Name;
                                }
                                exportable = ClassIDType.Shader.CanExport();
                                break;
                            case ClassIDType.Animator when ClassIDType.Animator.CanParse():
                                var component = new PPtr<Object>(objectReader);
                                animators.Add((component, asset));
                                asset.Name = objectReader.type.ToString();
                                exportable = ClassIDType.Animator.CanExport();
                                break;
                            case ClassIDType.MiHoYoBinData when ClassIDType.MiHoYoBinData.CanParse():
                                var MiHoYoBinData = new MiHoYoBinData(objectReader);
                                obj = MiHoYoBinData;
                                asset.Name = objectReader.type.ToString();
                                exportable = ClassIDType.MiHoYoBinData.CanExport();
                                break;
                            case ClassIDType.IndexObject when ClassIDType.IndexObject.CanParse():
                                var indexObject = new IndexObject(objectReader);
                                obj = null;
                                foreach (var index in indexObject.AssetMap)
                                {
                                    mihoyoBinDataNames.Add((index.Value.Object, index.Key));
                                }
                                asset.Name = "IndexObject";
                                exportable = ClassIDType.IndexObject.CanExport();
                                break;
                            case ClassIDType.MonoBehaviour when ClassIDType.MonoBehaviour.CanParse():
                                var mono = new MonoBehaviour(objectReader);
                                asset.Name = String.IsNullOrEmpty(mono.Name) ? objectReader.type.ToString() : mono.Name;
                                exportable = ClassIDType.MonoBehaviour.CanExport();
                                break;
                            case ClassIDType.MonoScript when ClassIDType.MonoScript.CanParse():
                                var mono_script = new MonoScript(objectReader);

                                asset.Name = String.IsNullOrEmpty(mono_script.m_Name) ? objectReader.type.ToString() : mono_script.m_Name;
                                exportable = ClassIDType.MonoScript.CanExport();
                                break;
                            case ClassIDType.Font when ClassIDType.Font.CanExport():
                            case ClassIDType.Material when ClassIDType.Material.CanExport():
                            case ClassIDType.Texture when ClassIDType.Texture.CanExport():
                            case ClassIDType.Mesh when ClassIDType.Mesh.CanExport():
                            case ClassIDType.Sprite when ClassIDType.Sprite.CanExport():
                            case ClassIDType.TextAsset when ClassIDType.TextAsset.CanExport():
                            case ClassIDType.Texture2D when ClassIDType.Texture2D.CanExport():
                            case ClassIDType.VideoClip when ClassIDType.VideoClip.CanExport():
                            case ClassIDType.AudioClip when ClassIDType.AudioClip.CanExport():
                            case ClassIDType.AnimationClip when ClassIDType.AnimationClip.CanExport():
                                asset.Name = objectReader.ReadAlignedString();
                                exportable = true;
                                break;
                            default:
                                asset.Name = objectReader.type.ToString();
                                exportable = !Minimal;
                                break;
                        }


                        if (obj != null)
                        {
                            objectAssetItemDic.Add(obj, asset);
                            assetsFile.AddObject(obj);
                        }
                        if (exportable)
                            matches.Add(asset);
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Unable to load assetmap for \n{assetsFile.fileName}\nType {objectReader?.type}\nPathID {objInfo.m_PathID}\n{e}");
                    }
                }
            }


            foreach ((var pptr, var asset) in animators)
            {
                if (pptr.TryGet<GameObject>(out var gameObject))
                {
                    asset.Name = gameObject.m_Name;
                    if (ClassIDType.GameObject.CanExport())
                    {
                        var tmp = new AssetEntry()
                        {
                            Source = file,
                            PathID = gameObject.m_PathID,
                            Type = gameObject.type,
                            Container = ""
                        };
                        tmp.Name = gameObject.m_Name;
                        if (!objectAssetItemDic.ContainsKey(gameObject))
                        {
                            objectAssetItemDic.Add(gameObject, tmp);
                            matches.Add(tmp);
                        }

                    }
                }
            }


            foreach (var (pptr, name) in mihoyoBinDataNames)
            {
                if (pptr.TryGet<MiHoYoBinData>(out var miHoYoBinData) && objectAssetItemDic.TryGetValue(miHoYoBinData, out var asset))
                {
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        asset.Name = name;
                        asset.Container = hash.ToString();
                    }
                    else
                    {
                        asset.Name = $"BinFile #{asset.PathID}";
                    }
                }
            }

            foreach ((var pptr, var container) in containers)
            {
                if (pptr.TryGet(out var obj))
                {
                    objectAssetItemDic[obj].Container = container;
                }
            }

            lock (assets)
            {
                assets.AddRange(matches.Where(x =>
                {
                    var matchRegex = nameFilters.IsNullOrEmpty() || nameFilters.Any(r => r.IsMatch(x.Name));
                    var matchType = typeFilters.IsNullOrEmpty() || typeFilters.Contains(x.Type);
                    var matchContainer = containerFilters.IsNullOrEmpty() || containerFilters.Any(r => r.IsMatch(x.Container));
                    return matchRegex && matchType && matchContainer;
                }));
            }
        }


    }
}
