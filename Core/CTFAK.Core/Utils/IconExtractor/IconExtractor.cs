using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ImageMagick;

namespace TsudaKageyu
{
    public class IconExtractor
    {
        private byte[][] iconData;

        public int Count => iconData?.Length ?? 0;

        public IconExtractor(string fileName)
        {
            iconData = ExtractIconsFromPE(fileName);
        }

        public MagickImage GetIcon(int index)
        {
            if (index < 0 || Count <= index)
                throw new ArgumentOutOfRangeException("index");

            using var ms = new MemoryStream(iconData[index]);
            return new MagickImage(ms);
        }

        public List<MagickImage> GetAllIcons()
        {
            var icons = new List<MagickImage>();
            for (int i = 0; i < Count; ++i)
                icons.Add(GetIcon(i));
            return icons;
        }

        private static byte[][] ExtractIconsFromPE(string fileName)
        {
            try
            {
                using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                fs.Seek(0x3C, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();

                fs.Seek(peOffset, SeekOrigin.Begin);
                uint peSignature = br.ReadUInt32();
                if (peSignature != 0x4550) return null;

                br.ReadUInt16();
                ushort numSections = br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt16();
                br.ReadUInt16();

                uint resourceVA = 0;
                uint resourceSize = 0;
                var sections = new List<(uint VA, uint Size, uint Pointer)>();
                for (int i = 0; i < numSections; i++)
                {
                    byte[] name = br.ReadBytes(8);
                    uint sizeOfRawData = br.ReadUInt32();
                    uint pointerToRawData = br.ReadUInt32();
                    br.ReadUInt32();
                    br.ReadUInt32();
                    br.ReadUInt32();
                    br.ReadUInt32();

                    sections.Add((resourceVA, sizeOfRawData, pointerToRawData));
                    if (name[0] == '.' && name[1] == 'r' && name[2] == 's' && name[3] == 'r' &&
                        name[4] == 'c')
                    {
                        resourceVA = sections[i].VA;
                        resourceSize = sections[i].Size;
                    }
                }

                if (resourceSize == 0) return null;

                fs.Seek((long)sections[0].Pointer, SeekOrigin.Begin);

                uint characteristics = br.ReadUInt32();
                uint timeDateStamp = br.ReadUInt32();
                ushort majorVersion = br.ReadUInt16();
                ushort minorVersion = br.ReadUInt16();
                ushort namedEntries = br.ReadUInt16();
                ushort idEntries = br.ReadUInt16();

                uint resourceDirSize = 16 * (uint)(namedEntries + idEntries) + 16;
                byte[] resourceDir = br.ReadBytes((int)resourceDirSize);

                var icoDataList = new List<byte[]>();

                for (int i = 0; i < namedEntries + idEntries; i++)
                {
                    int offset = 16 + 8 * i;
                    uint nameOrId = BitConverter.ToUInt32(resourceDir, offset);
                    uint subdirOffset = BitConverter.ToUInt32(resourceDir, offset + 4);
                    uint subdirRVA = subdirOffset & 0x7FFFFFFF;
                    int level1 = (int)(subdirOffset >> 31);

                    if (level1 != 0 || nameOrId != 3) continue;

                    int dirSize = ReadResourceDirectory(br, sections, subdirRVA, out List<(uint width, uint height, uint bitCount, byte[] imageData)> icons);
                    if (icons == null || icons.Count == 0) continue;

                    int icoSize = 6 + 16 * icons.Count;
                    foreach (var icon in icons)
                        icoSize += (int)icon.imageData.Length;

                    using var ms = new MemoryStream(icoSize);
                    using var bw = new BinaryWriter(ms);
                    bw.Write((ushort)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)icons.Count);

                    int picOffset = 6 + 16 * icons.Count;
                    for (int j = 0; j < icons.Count; j++)
                    {
                        var icon = icons[j];
                        bw.Write((byte)icon.width == 0 ? 256 : (byte)icon.width);
                        bw.Write((byte)icon.height == 0 ? 256 : (byte)icon.height);
                        bw.Write((byte)0);
                        bw.Write((byte)0);
                        bw.Write((ushort)1);
                        bw.Write((ushort)32);
                        bw.Write((uint)icon.imageData.Length);
                        bw.Write((uint)picOffset);
                        picOffset += (int)icon.imageData.Length;
                    }

                    foreach (var icon in icons)
                        bw.Write(icon.imageData);

                    icoDataList.Add(ms.ToArray());
                }

                return icoDataList.Count > 0 ? icoDataList.ToArray() : null;
            }
            catch
            {
                return null;
            }
        }

        private static int ReadResourceDirectory(BinaryReader br, List<(uint VA, uint Size, uint Pointer)> sections, uint rva,
            out List<(uint width, uint height, uint bitCount, byte[] imageData)> result)
        {
            result = null;
            long dirPos = br.BaseStream.Position;

            foreach (var section in sections)
            {
                if (rva >= section.VA && rva < section.VA + section.Size)
                {
                    br.BaseStream.Seek((long)(section.Pointer + (rva - section.VA)), SeekOrigin.Begin);
                    break;
                }
            }

            ushort characteristics = br.ReadUInt16();
            ushort timeDateStamp = br.ReadUInt16();
            ushort majorVersion = br.ReadUInt16();
            ushort minorVersion = br.ReadUInt16();
            ushort namedEntries = br.ReadUInt16();
            ushort idEntries = br.ReadUInt16();

            int dirSize = 16 * (namedEntries + idEntries) + 16;
            byte[] dirData = br.ReadBytes(dirSize);

            result = new List<(uint, uint, uint, byte[])>();
            for (int i = 0; i < namedEntries + idEntries; i++)
            {
                int offset = 16 + 8 * i;
                uint nameOrId = BitConverter.ToUInt32(dirData, offset);
                uint subdirOffset = BitConverter.ToUInt32(dirData, offset + 4);
                uint subdirRVA = subdirOffset & 0x7FFFFFFF;
                int level2 = (int)(subdirOffset >> 31);

                if (level2 != 1) continue;

                long dataPos = br.BaseStream.Position;
                int iconDirSize = ReadIconDirectory(br, sections, subdirRVA,
                    out uint width, out uint height, out uint bitCount, out byte[] imageData);
                br.BaseStream.Seek(dataPos, SeekOrigin.Begin);

                if (imageData != null && imageData.Length > 0)
                    result.Add((width, height, bitCount, imageData));
            }

            br.BaseStream.Seek(dirPos + 16 + 8 * (namedEntries + idEntries), SeekOrigin.Begin);
            return dirSize;
        }

        private static int ReadIconDirectory(BinaryReader br, List<(uint VA, uint Size, uint Pointer)> sections, uint rva,
            out uint width, out uint height, out uint bitCount, out byte[] imageData)
        {
            width = 0; height = 0; bitCount = 0; imageData = null;
            long dirPos = br.BaseStream.Position;

            foreach (var section in sections)
            {
                if (rva >= section.VA && rva < section.VA + section.Size)
                {
                    br.BaseStream.Seek((long)(section.Pointer + (rva - section.VA)), SeekOrigin.Begin);
                    break;
                }
            }

            ushort characteristics = br.ReadUInt16();
            ushort timeDateStamp = br.ReadUInt16();
            ushort majorVersion = br.ReadUInt16();
            ushort minorVersion = br.ReadUInt16();
            ushort namedEntries = br.ReadUInt16();
            ushort idEntries = br.ReadUInt16();

            int dirSize = 16 * (namedEntries + idEntries) + 16;
            byte[] dirData = br.ReadBytes(dirSize);

            for (int i = 0; i < namedEntries + idEntries; i++)
            {
                int offset = 16 + 8 * i;
                uint entryRVA = BitConverter.ToUInt32(dirData, offset);
                uint entrySize = BitConverter.ToUInt32(dirData, offset + 4);
                int level3 = (int)(entryRVA >> 31);

                if (level3 != 2) continue;

                uint dataRVA = entryRVA & 0x7FFFFFFF;
                foreach (var section in sections)
                {
                    if (dataRVA >= section.VA && dataRVA < section.VA + section.Size)
                    {
                        long dataOffset = (long)(section.Pointer + (dataRVA - section.VA));
                        br.BaseStream.Seek(dataOffset, SeekOrigin.Begin);

                        width = br.ReadByte();
                        height = br.ReadByte();
                        br.ReadByte();
                        br.ReadByte();
                        bitCount = br.ReadUInt16();
                        br.ReadUInt32();
                        uint actualSize = br.ReadUInt32();

                        imageData = br.ReadBytes((int)actualSize);
                        break;
                    }
                }
            }

            br.BaseStream.Seek(dirPos + 16 + 8 * (namedEntries + idEntries), SeekOrigin.Begin);
            return dirSize;
        }
    }
}
