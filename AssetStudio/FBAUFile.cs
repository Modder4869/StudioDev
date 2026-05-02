using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetStudio
{
    public class FBAUFile
    {
        private readonly byte[] _coreKey = new byte[16];

        private TCTUtils.TCT Game;

        public BundleFile.Header m_Header;
        private List<BundleFile.Node> m_DirectoryInfo;
        private List<BundleFile.StorageBlock> m_BlocksInfo;

        public List<StreamFile> fileList;

        public FBAUFile(FileReader reader, Game game)
        {
            Game = (TCTUtils.TCT)game;

            ReadHeader(reader);
            ReadBlocksInfoAndDirectory(reader);
            using var blocksStream = CreateBlocksStream(reader.FullPath);
            ReadBlocks(reader, blocksStream);
            ReadFiles(blocksStream, reader.FullPath);
        }

        private void ReadHeader(FileReader reader)
        {
            m_Header = new BundleFile.Header();

            m_Header.signature = reader.ReadStringToNull();
            Logger.Verbose($"Parsed signature {m_Header.signature}");
            if (m_Header.signature != "FBAU")
                throw new Exception("not a FBAU file");

            reader.ReadBytes(0x10).CopyTo(_coreKey, 0);

            var sm4 = new TCTUtils.SM4(Game.SM4Key, Game.SM4IV);
            sm4.Decrypt(_coreKey);

            var headerGen = Game.GetHeaderGenerator(_coreKey);

            m_Header.size = reader.ReadInt64() ^ headerGen.NextInt64();
            m_Header.flags = (ArchiveFlags)(reader.ReadUInt32() ^ headerGen.NextUInt32());
            m_Header.compressedBlocksInfoSize = reader.ReadUInt32() ^ headerGen.NextUInt32();

            var unityRevision = reader.ReadBytes(0x10);
            var unityRevisionKey = headerGen.NextBytes(0x10);
            for (int i = 0; i < unityRevision.Length; i++)
            {
                unityRevision[i] ^= unityRevisionKey[i];
            }

            m_Header.unityRevision = Encoding.UTF8.GetString(unityRevision);
            m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32() ^ headerGen.NextUInt32();
            m_Header.version = reader.ReadUInt32() ^ headerGen.NextUInt32();

            var unityVersion = reader.ReadBytes(8);
            var unityVersionKey = headerGen.NextBytes(8);
            for (int i = 0; i < unityVersion.Length; i++)
            {
                unityVersion[i] ^= unityVersionKey[i];
            }

            m_Header.unityVersion = Encoding.UTF8.GetString(unityVersion);

            Logger.Verbose($"Bundle header Info: {m_Header}");
        }

        private void ReadBlocksInfoAndDirectory(FileReader reader)
        {
            byte[] blocksInfoBytes;
            if (m_Header.version >= 7)
            {
                reader.AlignStream(16);
            }
            if ((m_Header.flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
            {
                var position = reader.Position;
                reader.Position = reader.BaseStream.Length - m_Header.compressedBlocksInfoSize;
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
                reader.Position = position;
            }
            else //0x40 BlocksAndDirectoryInfoCombined
            {
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
            }
            MemoryStream blocksInfoUncompresseddStream;

            var compressionType = (CompressionType)(m_Header.flags & ArchiveFlags.CompressionTypeMask);
            Logger.Verbose($"BlockInfo compression type: {compressionType}");
            switch (compressionType) //kArchiveCompressionTypeMask
            {
                case CompressionType.None: //None
                    {
                        blocksInfoUncompresseddStream = new MemoryStream(blocksInfoBytes);
                        break;
                    }
                default:
                    throw new IOException($"Unsupported compression type {compressionType}");
            }
            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompresseddStream, EndianType.BigEndian))
            {
                var blocksInfoGen = Game.GetBlocksInfoGenerator(_coreKey); 

                var blocksInfoCount = blocksInfoReader.ReadUInt32() ^ blocksInfoGen.NextUInt32();

                var uncompressedDataHash = blocksInfoReader.ReadBytes(16);
                var uncompressedDataHashKey = blocksInfoGen.NextBytes(16);
                for (int i = 0; i < uncompressedDataHash.Length; i++)
                {
                    uncompressedDataHash[i] ^= uncompressedDataHashKey[i];
                }

                m_BlocksInfo = new List<BundleFile.StorageBlock>();
                Logger.Verbose($"Blocks count: {blocksInfoCount}");
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    m_BlocksInfo.Add(new BundleFile.StorageBlock
                    {
                        flags = (StorageBlockFlags)(blocksInfoReader.ReadUInt16() ^ blocksInfoGen.NextUInt16()),
                        uncompressedSize = blocksInfoReader.ReadUInt32() ^ blocksInfoGen.NextUInt32(),
                        compressedSize = blocksInfoReader.ReadUInt32() ^ blocksInfoGen.NextUInt32()
                    });

                    Logger.Verbose($"Block {i} Info: {m_BlocksInfo[i]}");
                }

                var directoryInfoGen = Game.GetDirectoryInfoGenerator(_coreKey);

                var nodesCount = blocksInfoReader.ReadInt32() ^ directoryInfoGen.NextInt32();
                m_DirectoryInfo = new List<BundleFile.Node>();
                Logger.Verbose($"Directory count: {nodesCount}");
                for (int i = 0; i < nodesCount; i++)
                {
                    var node = new BundleFile.Node();
                    node.flags = blocksInfoReader.ReadUInt32() ^ directoryInfoGen.NextUInt32();
                    node.size = blocksInfoReader.ReadInt64() ^ directoryInfoGen.NextInt64();
                    var len = blocksInfoReader.ReadInt32() ^ directoryInfoGen.NextInt32();

                    var path = blocksInfoReader.ReadBytes(len);
                    var pathKey = directoryInfoGen.NextBytes(len);
                    for (int j = 0; j < path.Length; j++)
                    {
                        path[j] ^= pathKey[j];
                    }

                    node.path = Encoding.UTF8.GetString(path);
                    node.offset = blocksInfoReader.ReadInt64() ^ directoryInfoGen.NextInt64();
                    m_DirectoryInfo.Add(node);
                
                    Logger.Verbose($"Directory {i} Info: {m_DirectoryInfo[i]}");
                }
            }
            if ((m_Header.flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            {
                reader.AlignStream(16);
            }
        }

        private Stream CreateBlocksStream(string path)
        {
            Stream blocksStream;
            var uncompressedSizeSum = m_BlocksInfo.Sum(x => x.uncompressedSize);
            Logger.Verbose($"Total size of decompressed blocks: {uncompressedSizeSum}");
            if (uncompressedSizeSum >= int.MaxValue)
            {
                /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, uncompressedSizeSum);
                assetsDataStream = memoryMappedFile.CreateViewStream();*/
                blocksStream = new FileStream(path + ".temp", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            }
            else
            {
                blocksStream = new MemoryStream((int)uncompressedSizeSum);
            }
            return blocksStream;
        }

        public void ReadFiles(Stream blocksStream, string path)
        {
            Logger.Verbose($"Writing files from blocks stream...");

            fileList = new List<StreamFile>();
            for (int i = 0; i < m_DirectoryInfo.Count; i++)
            {
                var node = m_DirectoryInfo[i];
                var file = new StreamFile();
                fileList.Add(file);
                file.path = node.path;
                file.fileName = Path.GetFileName(node.path);
                if (node.size >= int.MaxValue)
                {
                    /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, entryinfo_size);
                    file.stream = memoryMappedFile.CreateViewStream();*/
                    var extractPath = path + "_unpacked" + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(extractPath);
                    file.stream = new FileStream(extractPath + file.fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                else
                {
                    file.stream = new MemoryStream((int)node.size);
                }
                blocksStream.Position = node.offset;
                blocksStream.CopyTo(file.stream, node.size);
                file.stream.Position = 0;

                if (node.flags == 4)
                {
                    DecryptAssetsFile(file);
                }
            }
        }

        private void DecryptAssetsFile(StreamFile file)
        {
            if (file.stream is MemoryStream memoryStream)
            {
                var buffer = memoryStream.GetBuffer();
                var ints = MemoryMarshal.Cast<byte, uint>(buffer);
                var version = BinaryPrimitives.ReverseEndianness(ints[2]);
                switch (version)
                {
                    case 0x64:
                        ints[0] ^= Game.HeaderConstants1;
                        ints[1] ^= Game.HeaderConstants2;
                        ints[3] ^= Game.HeaderConstants3;
                        ints[4] ^= Game.HeaderConstants4;

                        (ints[0], ints[1], ints[2], ints[3], ints[4]) = (ints[3], ints[4], BinaryPrimitives.ReverseEndianness((uint)0x11), ints[1], ints[0]);
                        break;
                    case 0x65:
                        var key = Game.GetHeaderKey(ints[3]);      
                        ulong iv = 0xC5DCFC3B0DAB120EUL;   // initialisation vector

                        for (int block = 0; block < 3; block++)
                        {
                            int idx = 4 + block * 2;
                            uint v0 = ints[idx];
                            uint v1 = ints[idx + 1];
                            ulong cipherBlock = ((ulong)v1 << 32) | v0; 
                            // XTEA or something
                            DecryptBlock(ref v0, ref v1, key);
                            ulong plainBlock = ((ulong)v1 << 32) | v0;
                            plainBlock ^= iv;
                            v0 = (uint)plainBlock;
                            v1 = (uint)(plainBlock >> 32);
                            iv = cipherBlock;
                            ints[idx] = v0;
                            ints[idx + 1] = v1;
                        }
                        buffer[0x28] ^= (byte)key[0];
                        var metadata = buffer.AsSpan(0x30);
                        if (BinaryPrimitives.ReadUInt32BigEndian(metadata) != 0x32303138) // 2018
                        {
                            var metadataKey = Game.GetMetadataKey(buffer);
                            for (int i = 0; i < BinaryPrimitives.ReverseEndianness(ints[5]); i++)
                            {
                                metadata[i] ^= metadataKey[i % metadataKey.Length];
                            }
                        }
                        break;
                    default:
                        throw new Exception($"Unsupported version {version}.");
                }
            }
        }
        private void DecryptBlock(ref uint v0, ref uint v1, uint[] key)
        {
            const uint delta = 0x11223355;
            const int rounds = 16;

            uint sum = unchecked(delta * (uint)(rounds - 1)); 

            for (int i = 0; i < rounds; i++)
            {
                uint t1 = (sum + delta + key[((sum + delta) >> 11) & 3])
                          ^ ((((v0 << 4) ^ (v0 >> 5)) + v0));
                v1 -= t1;

                uint t2 = (sum + key[sum & 3])
                          ^ ((((v1 << 4) ^ (v1 >> 5)) + v1));
                v0 -= t2;

                sum -= delta;
            }
        }
        
        private void ReadBlocks(FileReader reader, Stream blocksStream)
        {
            Logger.Verbose($"Writing block to blocks stream...");

            for (int i = 0; i < m_BlocksInfo.Count; i++)
            {
                Logger.Verbose($"Reading block {i}...");
                var blockInfo = m_BlocksInfo[i];
                var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
                Logger.Verbose($"Block compression type {compressionType}");
                switch (compressionType) //kStorageBlockCompressionTypeMask
                {
                    case CompressionType.None: //None
                        {
                            reader.BaseStream.CopyTo(blocksStream, blockInfo.compressedSize);
                            break;
                        }
                    case CompressionType.Lzma: //LZMA
                        {
                            var compressedStream = reader.BaseStream;
                            if (Game.Type.IsNetEase() && i == 0)
                            {
                                var compressedBytesSpan = reader.ReadBytes((int)blockInfo.compressedSize).AsSpan();
                                NetEaseUtils.DecryptWithoutHeader(compressedBytesSpan);
                                var ms = new MemoryStream(compressedBytesSpan.ToArray());
                                compressedStream = ms;
                            }
                            SevenZipHelper.StreamDecompress(compressedStream, blocksStream, blockInfo.compressedSize, blockInfo.uncompressedSize);
                            break;
                        }
                    case CompressionType.Lz4: //LZ4
                    case CompressionType.Lz4HC: //LZ4HC
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            try
                            {
                                var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                                var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                                reader.Read(compressedBytesSpan);
                                var numWrite = LZ4.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                                }
                                blocksStream.Write(uncompressedBytesSpan);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    default:
                        throw new IOException($"Unsupported compression type {compressionType}");
                }
            }
            blocksStream.Position = 0;
        }
    }
}