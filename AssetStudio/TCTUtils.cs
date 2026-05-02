using System;
using System.Buffers.Binary;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace AssetStudio;
public static class TCTUtils
{
    public interface IState
    {
        public byte[] NextBytes(int count);
    }
    public class Generator
    {
        private readonly IState _state;

        public Generator(IState state)
        {
            _state = state;
        }

        public virtual byte NextUInt8() => _state.NextBytes(1)[0];
        public virtual sbyte NextInt8() => (sbyte)_state.NextBytes(1)[0];
        public virtual ushort NextUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(_state.NextBytes(2));
        public virtual short NextInt16() => BinaryPrimitives.ReadInt16LittleEndian(_state.NextBytes(2));
        public virtual uint NextUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(_state.NextBytes(4));
        public virtual int NextInt32() => BinaryPrimitives.ReadInt32LittleEndian(_state.NextBytes(4));
        public virtual ulong NextUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(_state.NextBytes(8));
        public virtual long NextInt64() => BinaryPrimitives.ReadInt64LittleEndian(_state.NextBytes(8));
        public virtual byte[] NextBytes(int count) => _state.NextBytes(count);
    }
    public class XorPadGenerator : Generator
    {
        private readonly XorPad _xorPad;
        public XorPadGenerator(XorPad state) : base(state)
        {
            _xorPad = state;
        }

        public override ushort NextUInt16() => (ushort)NextUInt32();
        public override short NextInt16() => (short)NextUInt32();
        public override ulong NextUInt64() => (ulong)NextUInt32() << 32 | NextUInt32();
        public override long NextInt64() => (long)NextUInt64();
        public override byte[] NextBytes(int count)
        {
            var buffer = new byte[count];

            if (count % 4 != 0)
            {
                count += 4 - (count % 4); //align
            }

            count += 4; //null-terminated (1 byte then align);

            base.NextBytes(count).AsSpan(0, buffer.Length).CopyTo(buffer);

            return buffer;
        }
    }
    public class XorPad : IState
    {
        private readonly byte[] _state = new byte[64];
        private int _index;

        public XorPad(byte[] xorPad)
        {
            if (xorPad.Length != 0x40)
                throw new ArgumentException("Length be equal to 64 bytes !!", nameof(xorPad));

            xorPad.CopyTo(_state, 0);
        }

        public byte[] NextBytes(int count)
        {
            var buffer = new byte[count];

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = _state[_index++ % _state.Length];
            }

            return buffer;
        }
    }
    public class SM4
    {
        private static readonly byte[] _sbox = new byte[] { 0x60, 0xDA, 0x4A, 0xC9, 0x4D, 0xA7, 0x28, 0x41, 0xF2, 0x6F, 0xDC, 0x5B, 0x17, 0x54, 0x09, 0x2C, 0xA8, 0x2B, 0xAD, 0x31, 0x0D, 0xE0, 0xBB, 0x37, 0x6C, 0x24, 0xA3, 0x0F, 0x75, 0x45, 0x18, 0xEF, 0xEA, 0x7F, 0x06, 0x58, 0xFE, 0x88, 0xBC, 0xB7, 0x4B, 0xE5, 0x83, 0x2F, 0xA0, 0x32, 0xCE, 0x5D, 0xD7, 0x5A, 0xD5, 0x9A, 0x3F, 0x80, 0x81, 0xAE, 0x6B, 0x50, 0xED, 0x4F, 0x8B, 0x72, 0x56, 0xBE, 0xEB, 0x8F, 0x68, 0x4C, 0x6E, 0x01, 0x99, 0x89, 0xD0, 0x14, 0xF8, 0xE2, 0x1C, 0x53, 0x27, 0x66, 0x25, 0xB1, 0x8E, 0x19, 0x48, 0xD3, 0x9F, 0x84, 0x97, 0xE1, 0x64, 0x10, 0x6A, 0x5C, 0x42, 0x2A, 0xCB, 0x3B, 0x65, 0xBF, 0xB6, 0x52, 0x2E, 0x55, 0xF4, 0x62, 0x44, 0xA9, 0xBD, 0x95, 0x61, 0xB8, 0x5F, 0xC0, 0x23, 0x78, 0x8D, 0x91, 0xB0, 0xA1, 0xC4, 0x08, 0xAB, 0x79, 0xE6, 0xC8, 0xB4, 0x85, 0x82, 0x20, 0x0B, 0xDB, 0xC2, 0x8C, 0xD8, 0x51, 0x15, 0x11, 0xEE, 0x63, 0xB2, 0x1B, 0xDE, 0x7B, 0xFC, 0x2D, 0xA4, 0x5E, 0xF3, 0x90, 0x46, 0x59, 0xF6, 0xD1, 0x35, 0x33, 0x7C, 0x40, 0xA5, 0x73, 0xB3, 0x1D, 0x92, 0x0C, 0x1F, 0x29, 0xF0, 0x49, 0x39, 0x16, 0xD2, 0xE4, 0x9C, 0x13, 0x34, 0x21, 0x71, 0xD6, 0xE3, 0xF9, 0x69, 0xFF, 0xAA, 0xB9, 0xC3, 0xAF, 0x00, 0xA6, 0x3A, 0x9E, 0x07, 0x77, 0x1E, 0x9D, 0x87, 0xC1, 0x05, 0xC7, 0x57, 0xCD, 0x30, 0x6D, 0xFB, 0x74, 0x86, 0x96, 0xBA, 0xDD, 0xC5, 0x04, 0xF1, 0xAC, 0x02, 0xCA, 0xC6, 0x4E, 0x36, 0x47, 0x8A, 0xFA, 0x98, 0x7D, 0x1A, 0xD9, 0x12, 0xF5, 0x94, 0x0E, 0xD4, 0x7A, 0xE9, 0xF7, 0x76, 0x7E, 0x03, 0xA2, 0xDF, 0xE7, 0xCC, 0x67, 0xB5, 0xFD, 0x3C, 0x22, 0x38, 0x9B, 0x43, 0x26, 0x0A, 0x93, 0xEC, 0x3E, 0x3D, 0xE8, 0x70, 0xCF };
        private static readonly uint[] _ck = new uint[] { 0xD1A27, 0x34414E5B, 0x6875828F, 0x9CA9B6C3, 0xD0DDEAF7, 0x4111E2B, 0x3845525F, 0x6C798693, 0xA0ADBAC7, 0xD4E1EEFB, 0x815222F, 0x3C495663, 0x707D8A97, 0xA4B1BECB, 0xD8E5F2FF, 0xC192633, 0x404D5A67, 0x74818E9B, 0xA8B5C2CF, 0xDCE9F603, 0x101D2A37, 0x44515E6B, 0x7885929F, 0xACB9C6D3, 0xE0EDFA07, 0x14212E3B, 0x4855626F, 0x7C8996A3, 0xB0BDCAD7, 0xE4F1FE0B, 0x1825323F, 0x4C596673 };
        private static readonly uint[] _fk = new uint[] { 0xB9B7ED68, 0x71750A9F, 0xA6070525, 0x3AA8C2C5 };

        private readonly uint[] _iv = new uint[4]; // not used
        private readonly uint[] _state = new uint[32];

        public SM4(byte[] key, byte[] iv)
        {
            MemoryMarshal.Cast<byte, uint>(iv).CopyTo(_iv);

            var buffer = (stackalloc uint[4]);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(i * 4)) ^ _fk[i];
            }

            for (var i = 0; i < _state.Length; i++)
            {
                _state[i] = buffer[i % buffer.Length] ^= F(buffer, i);
            }
        }

        public void Decrypt(byte[] data)
        {
            if (data.Length != 0x10)
            {
                throw new ArgumentException("Size should be 16 bytes !!", nameof(data));
            }

            var buffer = (stackalloc uint[4]);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i * 4));
            }

            for (var i = 0; i < _state.Length; i++)
            {
                buffer[i % buffer.Length] ^= RK(buffer, i);
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(i * 4), buffer[^(i + 1)]);
            }
        }

        private uint RK(Span<uint> buffer, int i) => L2(S(_state[^(i + 1)] ^ buffer[(i + 1) % buffer.Length] ^ buffer[(i + 2) % buffer.Length] ^ buffer[(i + 3) % buffer.Length]));

        private static uint S(uint value)
        {
            var buffer = (stackalloc byte[4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            return BinaryPrimitives.ReadUInt32LittleEndian(new byte[] { _sbox[buffer[0]], _sbox[buffer[1]], _sbox[buffer[2]], _sbox[buffer[3]] });
        }
        private static uint F(Span<uint> buffer, int i) => L1(S(_ck[i] ^ buffer[(i + 1) % buffer.Length] ^ buffer[(i + 2) % buffer.Length] ^ buffer[(i + 3) % buffer.Length]));
        private static uint L1(uint value) => value ^ uint.RotateLeft(value, 13) ^ uint.RotateLeft(value, 23);
        private static uint L2(uint value) => value ^ uint.RotateLeft(value, 2) ^ uint.RotateLeft(value, 10) ^ uint.RotateLeft(value, 18) ^ uint.RotateLeft(value, 24);
    }
    public class ZUC : IState
    {
        private static readonly byte[] _s0 = new byte[] { 0x3E, 0x72, 0x5B, 0x47, 0xCA, 0xE0, 0x00, 0x33, 0x04, 0xD1, 0x54, 0x98, 0x09, 0xB9, 0x6D, 0xCB, 0x7B, 0x1B, 0xF9, 0x32, 0xAF, 0x9D, 0x6A, 0xA5, 0xB8, 0x2D, 0xFC, 0x1D, 0x08, 0x53, 0x03, 0x90, 0x4D, 0x4E, 0x84, 0x99, 0xE4, 0xCE, 0xD9, 0x91, 0xDD, 0xB6, 0x85, 0x48, 0x8B, 0x29, 0x6E, 0xAC, 0xCD, 0xC1, 0xF8, 0x1E, 0x73, 0x43, 0x69, 0xC6, 0xB5, 0xBD, 0xFD, 0x39, 0x63, 0x20, 0xD4, 0x38, 0x76, 0x7D, 0xB2, 0xA7, 0xCF, 0xED, 0x57, 0xC5, 0xF3, 0x2C, 0xBB, 0x14, 0x21, 0x06, 0x55, 0x9B, 0xE3, 0xEF, 0x5E, 0x31, 0x4F, 0x7F, 0x5A, 0xA4, 0x0D, 0x82, 0x51, 0x49, 0x5F, 0xBA, 0x58, 0x1C, 0x4A, 0x16, 0xD5, 0x17, 0xA8, 0x92, 0x24, 0x1F, 0x8C, 0xFF, 0xD8, 0xAE, 0x2E, 0x01, 0xD3, 0xAD, 0x3B, 0x4B, 0xDA, 0x46, 0xEB, 0xC9, 0xDE, 0x9A, 0x8F, 0x87, 0xD7, 0x3A, 0x80, 0x6F, 0x2F, 0xC8, 0xB1, 0xB4, 0x37, 0xF7, 0x0A, 0x22, 0x13, 0x28, 0x7C, 0xCC, 0x3C, 0x89, 0xC7, 0xC3, 0x96, 0x56, 0x07, 0xBF, 0x7E, 0xF0, 0x0B, 0x2B, 0x97, 0x52, 0x35, 0x41, 0x79, 0x61, 0xA6, 0x4C, 0x10, 0xFE, 0xBC, 0x26, 0x95, 0x88, 0x8A, 0xB0, 0xA3, 0xFB, 0xC0, 0x18, 0x94, 0xF2, 0xE1, 0xE5, 0xE9, 0x5D, 0xD0, 0xDC, 0x11, 0x66, 0x64, 0x5C, 0xEC, 0x59, 0x42, 0x75, 0x12, 0xF5, 0x74, 0x9C, 0xAA, 0x23, 0x0E, 0x86, 0xAB, 0xBE, 0x2A, 0x02, 0xE7, 0x67, 0xE6, 0x44, 0xA2, 0x6C, 0xC2, 0x93, 0x9F, 0xF1, 0xF6, 0xFA, 0x36, 0xD2, 0x50, 0x68, 0x9E, 0x62, 0x71, 0x15, 0x3D, 0xD6, 0x40, 0xC4, 0xE2, 0x0F, 0x8E, 0x83, 0x77, 0x6B, 0x25, 0x05, 0x3F, 0x0C, 0x30, 0xEA, 0x70, 0xB7, 0xA1, 0xE8, 0xA9, 0x65, 0x8D, 0x27, 0x1A, 0xDB, 0x81, 0xB3, 0xA0, 0xF4, 0x45, 0x7A, 0x19, 0xDF, 0xEE, 0x78, 0x34, 0x60 };
        private static readonly byte[] _s1 = new byte[] { 0x55, 0xC2, 0x63, 0x71, 0x3B, 0xC8, 0x47, 0x86, 0x9F, 0x3C, 0xDA, 0x5B, 0x29, 0xAA, 0xFD, 0x77, 0x8C, 0xC5, 0x94, 0x0C, 0xA6, 0x1A, 0x13, 0x00, 0xE3, 0xA8, 0x16, 0x72, 0x40, 0xF9, 0xF8, 0x42, 0x44, 0x26, 0x68, 0x96, 0x81, 0xD9, 0x45, 0x3E, 0x10, 0x76, 0xC6, 0xA7, 0x8B, 0x39, 0x43, 0xE1, 0x3A, 0xB5, 0x56, 0x2A, 0xC0, 0x6D, 0xB3, 0x05, 0x22, 0x66, 0xBF, 0xDC, 0x0B, 0xFA, 0x62, 0x48, 0xDD, 0x20, 0x11, 0x06, 0x36, 0xC9, 0xC1, 0xCF, 0xF6, 0x27, 0x52, 0xBB, 0x69, 0xF5, 0xD4, 0x87, 0x7F, 0x84, 0x4C, 0xD2, 0x9C, 0x57, 0xA4, 0xBC, 0x4F, 0x9A, 0xDF, 0xFE, 0xD6, 0x8D, 0x7A, 0xEB, 0x2B, 0x53, 0xD8, 0x5C, 0xA1, 0x14, 0x17, 0xFB, 0x23, 0xD5, 0x7D, 0x30, 0x67, 0x73, 0x08, 0x09, 0xEE, 0xB7, 0x70, 0x3F, 0x61, 0xB2, 0x19, 0x8E, 0x4E, 0xE5, 0x4B, 0x93, 0x8F, 0x5D, 0xDB, 0xA9, 0xAD, 0xF1, 0xAE, 0x2E, 0xCB, 0x0D, 0xFC, 0xF4, 0x2D, 0x46, 0x6E, 0x1D, 0x97, 0xE8, 0xD1, 0xE9, 0x4D, 0x37, 0xA5, 0x75, 0x5E, 0x83, 0x9E, 0xAB, 0x82, 0x9D, 0xB9, 0x1C, 0xE0, 0xCD, 0x49, 0x89, 0x01, 0xB6, 0xBD, 0x58, 0x24, 0xA2, 0x5F, 0x38, 0x78, 0x99, 0x15, 0x90, 0x50, 0xB8, 0x95, 0xE4, 0xD0, 0x91, 0xC7, 0xCE, 0xED, 0x0F, 0xB4, 0x6F, 0xA0, 0xCC, 0xF0, 0x02, 0x4A, 0x79, 0xC3, 0xDE, 0xA3, 0xEF, 0xEA, 0x51, 0xE6, 0x6B, 0x18, 0xEC, 0x1B, 0x2C, 0x80, 0xF7, 0x74, 0xE7, 0xFF, 0x21, 0x5A, 0x6A, 0x54, 0x1E, 0x41, 0x31, 0x92, 0x35, 0xC4, 0x33, 0x07, 0x0A, 0xBA, 0x7E, 0x0E, 0x34, 0x88, 0xB1, 0x98, 0x7C, 0xF3, 0x3D, 0x60, 0x6C, 0x7B, 0xCA, 0xD3, 0x1F, 0x32, 0x65, 0x04, 0x28, 0x64, 0xBE, 0x85, 0x9B, 0x2F, 0x59, 0x8A, 0xD7, 0xB0, 0x25, 0xAC, 0xAF, 0x12, 0x03, 0xE2, 0xF2 };
        private static readonly ushort[] _eKd = new ushort[] { 0x44D7, 0x26BC, 0x626B, 0x135E, 0x5789, 0x35E2, 0x7135, 0x09AF, 0x4D78, 0x2F13, 0x6BC4, 0x1AF1, 0x5E26, 0x3C4D, 0x789A, 0x47AC };

        private readonly uint[] _lfsr = new uint[16];
        private readonly uint[] _brx = new uint[4];
        private uint _fr1;
        private uint _fr2;

        private readonly byte[] _state = new byte[4];
        private int _index;

        public ZUC(byte[] key, byte[] iv)
        {
            for (int i = 0; i < _lfsr.Length; i++)
            {
                _lfsr[i] = Int31(key[i], _eKd[i], iv[i]);
            }

            _fr1 = 0;
            _fr2 = 0;

            for (int i = 0; i < 32; i++)
            {
                SwapBits();
                LFSR(F() >> 1);
            }

            SwapBits();
            F();
            LFSR();
        }

        public byte[] NextBytes(int count)
        {
            var buffer = new byte[count];

            for (int i = 0; i < buffer.Length; i++)
            {
                if (_index % 4 == 0)
                {
                    SwapBits();
                    BinaryPrimitives.WriteUInt32LittleEndian(_state, F() ^ _brx[3]);
                    LFSR();
                }

                buffer[i] = _state[_index++ % _state.Length];
            }

            return buffer;
        }

        private void SwapBits()
        {
            _brx[0] = ((_lfsr[15] & 0x7FFF8000) << 1) | (_lfsr[14] & 0xFFFF);
            _brx[1] = (_lfsr[11] & 0xFFFF) << 16 | _lfsr[9] >> 15;
            _brx[2] = (_lfsr[7] & 0xFFFF) << 16 | _lfsr[5] >> 15;
            _brx[3] = (_lfsr[2] & 0xFFFF) << 16 | _lfsr[0] >> 15;
        }

        private uint F()
        {
            var w = (_brx[0] ^ _fr1) + _fr2;

            var w1 = _fr1 + _brx[1];
            var w2 = _fr2 ^ _brx[2];

            _fr1 = S(L1((w1 << 16) | (w2 >> 16)));
            _fr2 = S(L2((w2 << 16) | (w1 >> 16)));

            return w;
        }

        private void LFSR(uint value = 0)
        {
            var state = _lfsr[0];

            state = AddM(state, RotateLeft31(_lfsr[0], 8));
            state = AddM(state, RotateLeft31(_lfsr[4], 20));
            state = AddM(state, RotateLeft31(_lfsr[10], 21));
            state = AddM(state, RotateLeft31(_lfsr[13], 17));
            state = AddM(state, RotateLeft31(_lfsr[15], 15));

            for (int i = 0; i < 15; ++i)
            {
                _lfsr[i] = _lfsr[i + 1];
            }

            _lfsr[15] = AddM(state, value);
        }

        private static uint S(uint value)
        {
            var buffer = (stackalloc byte[4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            return BinaryPrimitives.ReadUInt32LittleEndian(new byte[] { _s1[buffer[0]], _s0[buffer[1]], _s1[buffer[2]], _s0[buffer[3]] });
        }
        private static uint AddM(uint a, uint b) => ((a + b) & 0x7FFFFFFF) + ((a + b) >> 31);
        private static uint Int31(byte a, ushort b, byte c) => (uint)((a << 23) | (b << 8) | c);
        private static uint RotateLeft31(uint value, int offset) => ((value << offset) | (value >> (31 - offset))) & 0x7FFFFFFF;
        private static uint L1(uint value) => value ^ uint.RotateLeft(value, 2) ^ uint.RotateLeft(value, 10) ^ uint.RotateLeft(value, 18) ^ uint.RotateLeft(value, 24);
        private static uint L2(uint value) => value ^ uint.RotateLeft(value, 8) ^ uint.RotateLeft(value, 14) ^ uint.RotateLeft(value, 22) ^ uint.RotateLeft(value, 30);
    }
    public class ChaCha20 : IState
    {
        private readonly byte[] _context = new byte[64];
        private readonly uint[] _state = new uint[16];
        private int _index;

        public ChaCha20(byte[] constants, byte[] key, byte[] nonce, uint counter)
        {
            KeySetup(constants, key);
            IvSetup(nonce, counter);
        }

        private void KeySetup(byte[] constants, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(constants, nameof(constants));
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            if (constants.Length != 16)
            {
                throw new ArgumentException($"Length must be 32. Actual: {constants.Length}", nameof(constants));
            }

            if (key.Length != 32)
            {
                throw new ArgumentException($"Length must be 32. Actual: {key.Length}", nameof(key));
            }

            _state[0] = BinaryPrimitives.ReadUInt32LittleEndian(constants.AsSpan(0));
            _state[1] = BinaryPrimitives.ReadUInt32LittleEndian(constants.AsSpan(4));
            _state[2] = BinaryPrimitives.ReadUInt32LittleEndian(constants.AsSpan(8));
            _state[3] = BinaryPrimitives.ReadUInt32LittleEndian(constants.AsSpan(12));

            _state[4] = BinaryPrimitives.ReadUInt32LittleEndian(key);
            _state[5] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(4));
            _state[6] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(8));
            _state[7] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(12));
            _state[8] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(16));
            _state[9] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(20));
            _state[10] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(24));
            _state[11] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(28));
        }

        private void IvSetup(byte[] nonce, uint counter)
        {
            ArgumentNullException.ThrowIfNull(nonce, nameof(nonce));

            if (nonce.Length != 12)
            {
                throw new ArgumentException($"Length must be 12. Actual: {nonce.Length}", nameof(nonce));
            }

            _state[12] = counter;
            _state[13] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.AsSpan(0));
            _state[14] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.AsSpan(4));
            _state[15] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.AsSpan(8));
        }

        public byte[] NextBytes(int count)
        {
            var buffer = new byte[count];
            var x = new uint[16];

            for (int i = 0; i < count; i++)
            {
                if (_index % _context.Length == 0)
                {
                    for (int j = 0; j < _state.Length; j++)
                    {
                        x[j] = _state[j];
                    }

                    for (int j = 20; j > 0; j -= 2)
                    {
                        QuarterRound(x, 0, 4, 8, 12);
                        QuarterRound(x, 1, 5, 9, 13);
                        QuarterRound(x, 2, 6, 10, 14);
                        QuarterRound(x, 3, 7, 11, 15);

                        QuarterRound(x, 0, 5, 10, 15);
                        QuarterRound(x, 1, 6, 11, 12);
                        QuarterRound(x, 2, 7, 8, 13);
                        QuarterRound(x, 3, 4, 9, 14);
                    }

                    for (int j = 0; j < _state.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(_context.AsSpan(j * 4), x[j] + _state[j]);
                    }

                    _state[12]++;
                    if (_state[12] <= 0)
                    {
                        _state[13]++;
                    }
                }

                buffer[i] = _context[_index++ % _context.Length];
            }

            return buffer;
        }

        public static void QuarterRound(uint[] x, uint a, uint b, uint c, uint d)
        {

            if (x.Length != 16)
            {
                throw new ArgumentException($"Length must be 16. Actual: {x.Length}", nameof(x));
            }

            x[a] += x[b]; x[d] = BitOperations.RotateLeft(x[d] ^ x[a], 16);
            x[c] += x[d]; x[b] = BitOperations.RotateLeft(x[b] ^ x[c], 12);
            x[a] += x[b]; x[d] = BitOperations.RotateLeft(x[d] ^ x[a], 8);
            x[c] += x[d]; x[b] = BitOperations.RotateLeft(x[b] ^ x[c], 7);
        }
    }
    public class HC128 : IState
    {
        private readonly uint[] _p = new uint[512];
        private readonly uint[] _q = new uint[512];
        private int _counter;

        public HC128(byte[] key, byte[] iv)
        {
            ArgumentNullException.ThrowIfNull(nameof(key));
            ArgumentNullException.ThrowIfNull(nameof(iv));

            if (key.Length != 0x10)
                throw new ArgumentException("Length should be equal to 16 bytes !!", nameof(key));

            if (iv.Length != 0x10)
                throw new ArgumentException("Length should be equal to 16 bytes !!", nameof(iv));

            var w = new uint[1280];

            MemoryMarshal.Cast<byte, uint>(key).CopyTo(w);
            MemoryMarshal.Cast<byte, uint>(key).CopyTo(w.AsSpan(4));
            MemoryMarshal.Cast<byte, uint>(iv).CopyTo(w.AsSpan(8));
            MemoryMarshal.Cast<byte, uint>(iv).CopyTo(w.AsSpan(12));

            for (uint i = 16; i < w.Length; i++)
            {
                w[i] = F2(w[i - 2]) + w[i - 7] + F1(w[i - 15]) + w[i - 16] + i;
            }

            _counter = 0;
            w.AsSpan(256, 512).CopyTo(_p);
            w.AsSpan(768, 512).CopyTo(_q);
            for (int i = 0; i < 128; i++)
            {
                _p[i] = Step();
            }
        }

        public byte[] NextBytes(int count)
        {
            var buffer = new byte[count];

            var state = (stackalloc byte[64]);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (i % state.Length == 0)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(state[(j * 4)..], Step());
                    }
                }

                buffer[i] = state[i % state.Length];
            }

            return buffer;
        }

        private uint Step()
        {
            var a = _counter & 0x1FF;
            var b = (_counter - 3) & 0x1FF;
            var c = (_counter - 10) & 0x1FF;
            var d = (_counter - 12) & 0x1FF;
            var e = (_counter - 511) & 0x1FF;

            uint value;

            if (_counter < 512)
            {
                _p[a] += (uint.RotateRight(_p[b], 10) ^ uint.RotateRight(_p[e], 23)) + uint.RotateRight(_p[c], 8);
                value = H(_q, _p[d]) ^ _p[a];
            }
            else
            {
                _q[a] += (uint.RotateLeft(_q[b], 10) ^ uint.RotateLeft(_q[e], 23)) + uint.RotateLeft(_q[c], 8);
                value = H(_p, _q[d]) ^ _q[a];
            }

            _counter = (_counter + 1) % 0x400;
            return value;
        }

        private static uint H(uint[] a, uint b) => a[b % 0x100] + a[((b >> 16) % 0x100) + 0x100];
        private static uint F1(uint value) => (value >> 7) | (value << 25) | (value >> 18) | (value << 14) ^ (value >> 3);
        private static uint F2(uint value) => (value >> 17) | (value << 15) | (value >> 19) | (value << 13) ^ (value >> 10);
    }

    public abstract record TCT : Game
    {
        public byte[] SM4Key { get; }
        public byte[] SM4IV { get; }
        public byte[] ZUCKey { get; }
        public byte[] ZUCIV { get; }
        public byte[] ChaCha20Constants { get; }
        public byte[] ChaCha20Key { get; }
        public byte[] ChaCha20IV { get; }
        public byte[] HC128Key { get; }
        public byte[] HC128IV { get; }

        public uint HeaderConstants1 { get; protected set; }
        public uint HeaderConstants2 { get; protected set; }
        public uint HeaderConstants3 { get; protected set; }
        public uint HeaderConstants4 { get; protected set; }

        public uint[] HeaderKey { get; protected set; }
        public byte[] MetadataKey { get; protected set; }

        public TCT(GameType type, byte[] sm4Key, byte[] sm4IV, byte[] zucKey, byte[] zucIV, byte[] chacha20Constants, byte[] chacha20Key, byte[] chacha20IV, byte[] hc128Key, byte[] hc128IV) : base(type)
        {
            SM4Key = sm4Key;
            SM4IV = sm4IV;
            ZUCKey = zucKey;
            ZUCIV = zucIV;
            ChaCha20Constants = chacha20Constants;
            ChaCha20Key = chacha20Key;
            ChaCha20IV = chacha20IV;
            HC128Key = hc128Key;
            HC128IV = hc128IV;
        }

        public abstract Generator GetHeaderGenerator(byte[] coreKey);
        public abstract Generator GetBlocksInfoGenerator(byte[] coreKey);
        public abstract Generator GetDirectoryInfoGenerator(byte[] coreKey);
        public abstract uint[] GetHeaderKey(uint seed);
        public abstract byte[] GetMetadataKey(Span<byte> header);
    }

    public record TFTCN : TCT
    {
        public TFTCN(GameType type, byte[] sm4Key, byte[] sm4IV, byte[] zucKey, byte[] zucIV, byte[] chacha20Constants, byte[] chacha20Key, byte[] chacha20IV, byte[] hc128Key, byte[] hc128IV) : base(type, sm4Key, sm4IV, zucKey, zucIV, chacha20Constants, chacha20Key, chacha20IV, hc128Key, hc128IV)
        {
            HeaderConstants1 = 0x54514C43;
            HeaderConstants2 = 0x6A267E96;
            HeaderConstants3 = 0xB8E1AFED;
            HeaderConstants4 = 0xD01ADFB7;
        }
        public override Generator GetHeaderGenerator(byte[] coreKey)
        {
            var key = ZUCKey.ToArray();
            var iv = ZUCIV.ToArray();

            for (int i = 0; i < coreKey.Length; i++)
            {
                key[i] *= coreKey[i];
                iv[i] += (byte)(key[i] * coreKey[^(i + 1)]);
            }

            var zuc = new ZUC(key, iv);
            var xorPad = new XorPad(zuc.NextBytes(0x40));

            return new XorPadGenerator(xorPad);
        }

        public override Generator GetBlocksInfoGenerator(byte[] coreKey)
        {
            var key = ChaCha20Key.ToArray();
            var iv = ChaCha20IV.ToArray();

            for (int i = 0; i < coreKey.Length; i++)
            {
                key[i] ^= coreKey[i];
                iv[i] -= (byte)(key[i] * coreKey[^(i + 1)]);
            }

            var chacha20 = new ChaCha20(ChaCha20Constants, key, iv[..12], 0);

            return new Generator(chacha20);
        }

        public override Generator GetDirectoryInfoGenerator(byte[] coreKey)
        {
            var key = HC128Key.ToArray();
            var iv = HC128IV.ToArray();

            for (int i = 0; i < coreKey.Length; i++)
            {
                key[i] *= coreKey[i];
                iv[i] += (byte)(key[i] - coreKey[^(i + 1)]);
            }

            var hc128 = new HC128(key, iv);

            return new Generator(hc128);
        }

        public override uint[] GetHeaderKey(uint seed)
        {
            throw new NotImplementedException();
        }
        public override byte[] GetMetadataKey(Span<byte> header)
        {
            throw new NotImplementedException();
        }
    }

    public record WildRift : TCT
    {
        public WildRift(GameType type, byte[] sm4Key, byte[] sm4IV, byte[] zucKey, byte[] zucIV, byte[] chacha20Constants, byte[] chacha20Key, byte[] chacha20IV, byte[] hc128Key, byte[] hc128IV) : base(type, sm4Key, sm4IV, zucKey, zucIV, chacha20Constants, chacha20Key, chacha20IV, hc128Key, hc128IV)
        {
            HeaderConstants1 = 0xB9B7ED68;
            HeaderConstants2 = 0x71750A9F;
            HeaderConstants3 = 0xA6070525;
            HeaderConstants4 = 0x3AA8C2C5;
            HeaderKey = new uint[] { 0xD19AA1DD, 0x30D76717, 0x536D3AA7, 0xFE1BC68B };
            MetadataKey = new byte[] { 0xDF, 0x3C, 0xF3, 0x2A, 0x76, 0x3C, 0x2A, 0xAF, 0x26, 0x2D, 0x68, 0x5B, 0x48, 0xE1, 0x6B, 0xE7 };

            //HeaderConstants1 = 0x54514C43;
            //HeaderConstants2 = 0x2FFD72DB;
            //HeaderConstants3 = 0x98DFB5AC;
            //HeaderConstants4 = 0xD1310BA6;

            //HeaderKey = new uint[] { 0x636920D8, 0x71574E69, 0xA458FEA3, 0xF4933D7E };
            //MetadataKey = new byte[] { 0xF2, 0x58, 0x6F, 0x8F, 0xFD, 0x91, 0x7A, 0x31, 0x9A, 0xC6, 0xB3, 0x62, 0x8A, 0xA6, 0xF0, 0xB5 };
        }
        public override Generator GetHeaderGenerator(byte[] coreKey)
        {
            var key = ZUCKey.ToArray();
            var iv = ZUCIV.ToArray();

            for (int i = 0; i < coreKey.Length; i++)
            {
                iv[i] = (byte)((key[i] * iv[i]) - (coreKey[i] * iv[i]) + (coreKey[^(i + 1)] * iv[i]));
                key[i] -= coreKey[i];
            }

            var zuc = new ZUC(key, iv);
            var xorPad = new XorPad(zuc.NextBytes(0x40));

            return new XorPadGenerator(xorPad);
        }

        public override Generator GetBlocksInfoGenerator(byte[] coreKey)
        {
            var key = HC128Key.ToArray();
            var iv = HC128IV.ToArray();

            for (int i = 0; i < coreKey.Length; i++)
            {
                key[i] += (byte)(coreKey[i] << 1);
                iv[i] = (byte)((key[i] * iv[i]) - (coreKey[^(i + 1)] * iv[i]));
            }

            var hc128 = new HC128(key, iv);

            return new Generator(hc128);
        }

        public override Generator GetDirectoryInfoGenerator(byte[] coreKey)
        {
            var key = ChaCha20Key.ToArray();
            var iv = ChaCha20IV.ToArray();

            for (int i = 0; i < coreKey.Length; i++)
            {
                key[i] *= (byte)(coreKey[i] * 0x1C);
                iv[i] += (byte)(key[i] + (coreKey[^(i + 1)] * 0xDD));
            }

            var chacha20 = new ChaCha20(ChaCha20Constants, key, iv[..12], 0);

            return new Generator(chacha20);
        }

        public override uint[] GetHeaderKey(uint seed)
        {
            Console.WriteLine($"GetHeaderKey {seed}");
            var key = HeaderKey.ToArray();

            for (int i = 0; i < key.Length; i++)
            {
                key[i] ^= seed;
            }

            return key;
        }

        public override byte[] GetMetadataKey(Span<byte> header)
        {
            byte key = 0;
            var metadataKey = MetadataKey.ToArray();

            for (int i = 0; i < 0x30; i++)
            {
                key ^= header[i];
            }

            for (int i = 0; i < metadataKey.Length; i++)
            {
                metadataKey[i] ^= key;
            }

            return metadataKey;
        }
    }
}
