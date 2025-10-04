using System.Collections.Generic;

namespace AssetStudio
{
    public class AssetInfo
    {
        public int preloadIndex;
        public int preloadSize;
        public PPtr<Object> asset;
        public string address;
        public AssetInfo(ObjectReader reader)
        {
            preloadIndex = reader.ReadInt32();
            preloadSize = reader.ReadInt32();
            asset = new PPtr<Object>(reader);
            if (reader.Game.Type.isThreeKingdoms())
            {
                address = reader.ReadAlignedString();
            }
        }
    }

    public sealed class AssetBundle : NamedObject
    {
        public List<PPtr<Object>> m_PreloadTable;
        public List<KeyValuePair<string, AssetInfo>> m_Container;

        public AssetBundle(ObjectReader reader) : base(reader)
        {
            var m_PreloadTableSize = reader.ReadInt32();
            m_PreloadTable = new List<PPtr<Object>>();
            for (int i = 0; i < m_PreloadTableSize; i++)
            {
                m_PreloadTable.Add(new PPtr<Object>(reader));
            }

            var m_ContainerSize = reader.ReadInt32();
            m_Container = new List<KeyValuePair<string, AssetInfo>>();
            for (int i = 0; i < m_ContainerSize; i++)
            {
                if (reader.Game.Type.isThreeKingdoms())
                {
                    var first = reader.ReadUInt64().ToString();
                    var second = new AssetInfo(reader);
                    first = second.address != "" ? second.address : first;
                    m_Container.Add(new KeyValuePair<string, AssetInfo>(first, second));

                }
                else
                {
                    m_Container.Add(new KeyValuePair<string, AssetInfo>(reader.ReadAlignedString(), new AssetInfo(reader)));
                }
            }
        }
    }
}
