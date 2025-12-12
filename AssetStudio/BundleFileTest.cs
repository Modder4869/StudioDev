using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AssetStudio.BundleFile;
namespace AssetStudio
{
    public class BundleFilePartial : BundleFile
    {
        public BundleFilePartial(FileReader reader, Game game, bool partial = true)
      : base(reader, game, partial: partial, readBlocks: false)
        {

        }
 
    }
    public class SubStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public SubStream(Stream baseStream, long start, long length)
        {
            _baseStream = baseStream;
            _start = start;
            _length = length;
            _position = 0;
            _baseStream.Position = _start;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length) return 0;
            long remaining = _length - _position;
            int toRead = (int)Math.Min(count, remaining);

            lock (_baseStream)  // ensure thread safety if needed
            {
                _baseStream.Position = _start + _position;
                int read = _baseStream.Read(buffer, offset, toRead);
                _position += read;
                return read;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException()
            };
            if (newPos < 0 || newPos > _length) throw new ArgumentOutOfRangeException();
            _position = newPos;
            return _position;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public class BundleBlockCache
    {
        private readonly BundleFilePartial _bundle;
        private readonly FileReader _reader;
        private readonly List<StorageBlock> _allBlocks;

        private readonly HashSet<int> _alreadyDecompressed;
        private readonly Dictionary<int, byte[]> _compressedBlocks = new();
        private readonly Dictionary<int, long> _blockOffsets = new();
        private long _currentEnd = 0;

        private List<int> _resourceBlocks;
        public byte[] _firstBlockPrepend;
        private long _resourceStartOffset;
        private string _debugDumpFolder;

        private readonly Dictionary<(long offset, long size), Stream> _rangeCache = new();

        // Underlying decompressed stream holding all decompressed blocks
        private readonly MemoryStream _fullStream = new();

        public BundleBlockCache(BundleFilePartial bundle, FileReader reader,
            List<StorageBlock> remainingBlocks, List<int> remainingIndices,
            List<int> filteredIndices)
        {
            _bundle = bundle;
            _reader = reader;
            _allBlocks = bundle.m_BlocksInfo;
            _alreadyDecompressed = new HashSet<int>(filteredIndices);

            long acc = 0;
            for (int i = 0; i < remainingBlocks.Count; i++)
            {
                int index = remainingIndices[i];
                _compressedBlocks[index] = reader.ReadBytes((int)remainingBlocks[i].compressedSize);
                acc += remainingBlocks[i].compressedSize;
            }
        }

        public void SetResSBlocks(List<int> resourceBlocks, long startOffset = 0, byte[] firstBlockPrepend = null)
        {
            _resourceBlocks = resourceBlocks;
            _resourceStartOffset = startOffset;
            _firstBlockPrepend = firstBlockPrepend;
        }

        public void EnableRangeBlockDump(string folderPath)
        {
            _debugDumpFolder = folderPath;
            Directory.CreateDirectory(folderPath);
        }

        private void DecompressBlock(int blockIndex)
        {
            if (_blockOffsets.ContainsKey(blockIndex) || _alreadyDecompressed.Contains(blockIndex))
                return;

            var block = _allBlocks[blockIndex];
            var compressed = _compressedBlocks[blockIndex];

            using var compStream = new MemoryStream(compressed);
            using var outStream = new MemoryStream((int)block.uncompressedSize);

            var backup = _bundle.m_BlocksInfo;
            _bundle.m_BlocksInfo = new List<StorageBlock> { block };
            _bundle.ReadBlocks(new FileReader(_reader.FullPath, compStream, _reader.Game), outStream, (uint)blockIndex);
            _bundle.m_BlocksInfo = backup;

            long start = _currentEnd;
            _blockOffsets[blockIndex] = start;

            outStream.Position = 0;
            _fullStream.Position = start;
            outStream.CopyTo(_fullStream);
            _currentEnd = _fullStream.Length;

            //if (!string.IsNullOrEmpty(_debugDumpFolder))
            //{
            //    string outPath = Path.Combine(_debugDumpFolder, $"block_{blockIndex}.bin");
            //    File.WriteAllBytes(outPath, outStream.ToArray());
            //}
        }

        public (int blockIndex, long localOffset) GetBlockForResourceOffset(long offset)
        {
            if (_resourceBlocks == null || _resourceBlocks.Count == 0)
                return (-1, offset); // only overlap

            long accumulated = 0;
            int prependLen = _firstBlockPrepend?.Length ?? 0;
            bool first = true;

            foreach (var index in _resourceBlocks)
            {
                var blockSize = _allBlocks[index].uncompressedSize;
                if (first)
                {
                    blockSize += (uint)prependLen;
                    first = false;
                }

                long blockStart = accumulated;
                long blockEnd = accumulated + blockSize;

                if (offset < blockEnd)
                {
                    long local = offset - blockStart;
                    if (index == _resourceBlocks[0] && prependLen > 0)
                    {
                        if (local < prependLen)
                            return (index, local); // inside prepend
                        return (index, local - prependLen); // inside block
                    }
                    return (index, local);
                }

                accumulated = blockEnd;
            }

            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        public List<int> GetResourceBlocksForRange(long startOffset, long size)
        {
            long endOffset = startOffset + size;
            long accumulated = 0;
            var blocks = new List<int>();

            bool first = true;
            int prependLen = _firstBlockPrepend?.Length ?? 0;

            if (_resourceBlocks == null) return blocks;

            foreach (var index in _resourceBlocks)
            {
                long blockSize = _allBlocks[index].uncompressedSize;
                if (first)
                {
                    blockSize += prependLen;
                    first = false;
                }

                long blockStart = accumulated;
                long blockEnd = accumulated + blockSize;

                if (endOffset > blockStart && startOffset < blockEnd)
                    blocks.Add(index);

                accumulated = blockEnd;
            }

            return blocks;
        }

        public Stream GetBlockStream(int blockIndex)
        {
            DecompressBlock(blockIndex);
            long start = _blockOffsets[blockIndex];
            long length = _allBlocks[blockIndex].uncompressedSize;
            return new SubStream(_fullStream, start, length);
        }

        public Stream GetRangeStream(long offset, long size)
        {
            if (_rangeCache.TryGetValue((offset, size), out var cached))
            {
                cached.Position = 0;
                return cached;
            }

            var stream = new LazyRangeStream(this, offset, size);
            _rangeCache[(offset, size)] = stream;
            return stream;
        }

        public class LazyRangeStream : Stream
        {
            private readonly BundleBlockCache _cache;
            private readonly long _offset;
            private readonly long _length;
            private long _position;

            public LazyRangeStream(BundleBlockCache cache, long offset, long length)
            {
                _cache = cache;
                _offset = offset;
                _length = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _length) return 0;
                int toRead = (int)Math.Min(count, _length - _position);
                long absPos = _offset + _position;
                long written = 0;

                while (toRead > 0)
                {
                    var (blockIndex, localOffset) = _cache.GetBlockForResourceOffset(absPos);

                    if (blockIndex == -1)
                    {
                        if (_cache._firstBlockPrepend == null) break;
                        int prependRead = (int)Math.Min(toRead, _cache._firstBlockPrepend.Length - absPos);
                        if (prependRead <= 0) break;

                        Array.Copy(_cache._firstBlockPrepend, (int)absPos, buffer, offset + written, prependRead);
                        written += prependRead;
                        toRead -= prependRead;
                        _position += prependRead;
                        absPos += prependRead;
                        continue;
                    }

                    var blockStream = _cache.GetBlockStream(blockIndex);
                    long blockRemaining = blockStream.Length - localOffset;
                    int chunk = (int)Math.Min(toRead, blockRemaining);

                    blockStream.Position = localOffset;
                    int read = blockStream.Read(buffer, offset + (int)written, chunk);
                    if (read <= 0) break;

                    written += read;
                    toRead -= read;
                    _position += read;
                    absPos += read;
                }

                return (int)written;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _length + offset,
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (newPos < 0 || newPos > _length) throw new ArgumentOutOfRangeException();
                _position = newPos;
                return _position;
            }

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        public class SubStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _start;
            private readonly long _length;
            private long _position;

            public SubStream(Stream baseStream, long start, long length)
            {
                _baseStream = baseStream;
                _start = start;
                _length = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _length) return 0;
                int toRead = (int)Math.Min(count, _length - _position);

                lock (_baseStream)
                {
                    _baseStream.Position = _start + _position;
                    int read = _baseStream.Read(buffer, offset, toRead);
                    _position += read;
                    return read;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _length + offset,
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (newPos < 0 || newPos > _length) throw new ArgumentOutOfRangeException();
                _position = newPos;
                return _position;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}


