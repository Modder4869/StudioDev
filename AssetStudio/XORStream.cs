using System;
using System.IO;

namespace AssetStudio
{
    public class XORStream : OffsetStream
    {
        private readonly byte[] _xorpad;
        private readonly long _offset;

        private long Index => AbsolutePosition - _offset;

        public XORStream(Stream stream, long offset, byte[] xorpad) : base(stream, offset)
        {
            _xorpad = xorpad;
            _offset = offset;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var pos = Index;
            var read = base.Read(buffer, offset, count);
            if (pos >= 0)
            {
                for (int i = offset; i < count; i++)
                {
                    buffer[i] ^= _xorpad[pos++ % _xorpad.Length];
                }
            }
            return read;
        }
    }
    // Thanks to Razmoth for help and idea
    public class GF2Stream : OffsetStream
    {
        private readonly byte[] _key;
        private readonly int _length;
        public GF2Stream(Stream stream, byte[] key, int length) : base(stream, 0)
        {
            _key = key;
            _length = length;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var pos = Position;
            var read = base.Read(buffer, offset, count);

            int i = offset;
            while (pos < _length && i < read)
            {
                buffer[i++] ^= _key[pos++ % _key.Length];
            }

            return read;
        }
    }
    public class CCSakuraStream : OffsetStream
    {
        private readonly int _size;
        private readonly byte[] _key;
        public CCSakuraStream(Stream stream) : base(stream, 0)
        {
            var size = (int)Math.Min(Length / 2, 0x400);

            var pos = Position;
            Seek(-size, SeekOrigin.End);

            _key = new byte[size];
            base.Read(_key, 0, size);

            Position = pos;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var pos = Position;
            var read = base.Read(buffer, offset, count);

            int i = offset;
            while (pos < _key.Length && i < read)
            {
                buffer[i++] ^= _key[((_key.Length - 1) - pos++) % _key.Length];
            }

            return read;
        }
    }

}
