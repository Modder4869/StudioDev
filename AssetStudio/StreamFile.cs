using System.IO;

namespace AssetStudio
{
    public class StreamFile
    {
        public string path;
        public string fileName;
        public Stream stream;
        internal long offset;
        internal long size;
    }
}
