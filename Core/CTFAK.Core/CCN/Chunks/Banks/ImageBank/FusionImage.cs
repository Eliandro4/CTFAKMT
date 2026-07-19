using CTFAK.CCN.Chunks;
using CTFAK.Core.Utils;
using CTFAK.Memory;
using CTFAK.Utils;
using ImageMagick;
using K4os.Compression.LZ4;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CTFAK.Core.CCN.Chunks.Banks.ImageBank
{
    public class FusionImage : ChunkLoader
    {
        public int Handle;

        public int Width;
        public int Height;

        public short ActionX;
        public short ActionY;
        public short HotspotX;
        public short HotspotY;

        public byte[] imageData;

        public int Checksum;

        public BitDict Flags = new(new[]
        {
            "RLE",
            "RLEW",
            "RLET",
            "LZX",
            "Alpha",
            "ACE",
            "Mac",
            "RGBA"
        });

        public byte GraphicMode;

        public bool IsMFA;
        public byte[] newImageData;

        public int onepointfiveDecompressedSize;
        public int onepointfiveStart;
        public MagickImage realBitmap;
        public int references;
        public Color Transparent;
        public static bool logged = false;

        public MagickImage bitmap
        {
            get
            {
                if (realBitmap == null)
                {
                    realBitmap = new MagickImage(MagickColors.Transparent, (uint)Width, (uint)Height);
                    realBitmap.Format = MagickFormat.Rgba;

                    byte[] colorArray = null;

                    switch (GraphicMode)
                    {
                        case 0:
                            colorArray = ImageTranslator.AndroidMode0ToRGBA(imageData, Width, Height, false);
                            break;
                        case 1:
                            colorArray = ImageTranslator.AndroidMode1ToRGBA(imageData, Width, Height, false);
                            break;
                        case 2:
                            colorArray = ImageTranslator.AndroidMode2ToRGBA(imageData, Width, Height, false);
                            break;
                        case 3:
                            colorArray = ImageTranslator.AndroidMode3ToRGBA(imageData, Width, Height, false);
                            break;
                        case 4:
                            if (Settings.Android)
                                colorArray = ImageTranslator.AndroidMode4ToRGBA(imageData, Width, Height, false);
                            else
                                colorArray = ImageTranslator.Normal24BitMaskedToRGBA(imageData, Width, Height, Flags["Alpha"], Transparent, Settings.F3);
                            break;
                        case 5:
                            colorArray = ImageTranslator.AndroidMode5ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                            break;
                        case 6:
                            colorArray = ImageTranslator.Normal15BitToRGBA(imageData, Width, Height, false, Transparent);
                            break;
                        case 7:
                            colorArray = ImageTranslator.Normal16BitToRGBA(imageData, Width, Height, Flags["Alpha"], Transparent);
                            break;
                        case 8:
                            colorArray = ImageTranslator.TwoFivePlusToRGBA(imageData, Width, Height, Flags["Alpha"], Transparent, Flags["RGBA"], Settings.Fusion3Seed);
                            break;
                    }
                    if (colorArray == null)
                        Logger.LogWarning("colorArray is null for image mode " + GraphicMode);

                    int stride = Width * 4;
                    var pixels = realBitmap.GetPixels();
                    for (int y = 0; y < Height; y++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            int idx = (y * stride) + (x * 4);
                            pixels.SetPixel(x, y, new byte[] { colorArray[idx + 2], colorArray[idx + 1], colorArray[idx + 0], colorArray[idx + 3] });
                        }
                    }
                }

                return realBitmap;
            }
        }

        public void FromBitmap(MagickImage bmp)
        {
            Width = (int)bmp.Width;
            Height = (int)bmp.Height;
            if (CTFAKCore.parameters.Contains("-noalpha"))
                Flags["Alpha"] = false;
            GraphicMode = 4;

            var pixels = bmp.GetPixels();
            int copyPad = ImageHelper.GetPadding(Width, 4);

            imageData = new byte[Width * Height * 6];
            var position = 0;
            var pad = ImageHelper.GetPadding(Width, 3);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var pixel = pixels.GetPixel(x, y);
                    imageData[position] = pixel.GetChannel(2);
                    imageData[position + 1] = pixel.GetChannel(1);
                    imageData[position + 2] = pixel.GetChannel(0);
                    position += 3;
                }

                position += 3 * pad;
            }

            try
            {
                var aPad = ImageHelper.GetPadding(Width, 1, 4);
                var alphaPos = position;
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var pixel = pixels.GetPixel(x, y);
                        imageData[alphaPos] = pixel.GetChannel(3);
                        alphaPos += 1;
                    }

                    alphaPos += aPad;
                }
            }
            catch
            {
            }
        }

        public override void Read(ByteReader reader)
        {

            var start = reader.Tell();
            var dataSize = 0;
            if (Settings.Android)
            {
                Handle = reader.ReadInt16();

                switch (Handle >> 16)
                {
                    case 0:
                        GraphicMode = 0;
                        break;
                    case 3:
                        GraphicMode = 2;
                        break;
                    case 5:
                        GraphicMode = 7;
                        break;
                }

                if (Settings.Build >= 284 && !IsMFA)
                    Handle--;
                GraphicMode = (byte)reader.ReadInt32();
                Width = reader.ReadInt16();
                Height = reader.ReadInt16();
                HotspotX = reader.ReadInt16();
                HotspotY = reader.ReadInt16();
                ActionX = reader.ReadInt16();
                ActionY = reader.ReadInt16();
                dataSize = reader.ReadInt32();

                if (reader.PeekByte() == 255)
                    imageData = reader.ReadBytes(dataSize);
                else
                    imageData = Decompressor.DecompressBlock(reader, dataSize);

                return;

                // couldn't care less
            }

            Handle = reader.ReadInt32();
            if (Settings.Build >= 284 && !IsMFA)
                Handle--;

            if (!IsMFA)
            {
                if (Settings.Old)
                {
                    onepointfiveDecompressedSize = reader.ReadInt32();
                    onepointfiveStart = (int)reader.Tell();
                    newImageData = reader.ReadBytes();
                }
                else
                {
                    var decompressedSize = reader.ReadInt32();
                    var compSize = reader.ReadInt32();
                    newImageData = reader.ReadBytes(compSize);
                }
            }

            var mainRead = new Task(() =>
            {
                ByteReader decompressedReader;
                if (!IsMFA)
                {
                    if (Settings.Old)
                    {
                        decompressedReader = new ByteReader(Decompressor.DecompressOldBlock(newImageData,
                            newImageData.Length, onepointfiveDecompressedSize, out var actualSize));
                        reader.Seek(onepointfiveStart + actualSize);
                    }

                    else
                    {
                        decompressedReader =
                            new ByteReader(Decompressor.DecompressBlock(newImageData));
                    }

                    newImageData = null;
                }
                else
                {
                    decompressedReader = reader;
                }


                if (Settings.Old)
                    Checksum = decompressedReader.ReadInt16();
                else
                    Checksum = decompressedReader.ReadInt32();
                references = decompressedReader.ReadInt32();
                if (Settings.TwoFivePlus)
                    decompressedReader.Skip(4);
                dataSize = decompressedReader.ReadInt32();
                if (IsMFA)
                    decompressedReader = new ByteReader(decompressedReader.ReadBytes(dataSize + 20));
                Width = (int)decompressedReader.ReadInt16();
                Height = (int)decompressedReader.ReadInt16();
                GraphicMode = decompressedReader.ReadByte();
                Flags.flag = decompressedReader.ReadByte();
                if (!Settings.Old)
                    decompressedReader.ReadInt16();
                HotspotX = decompressedReader.ReadInt16();
                HotspotY = decompressedReader.ReadInt16();
                ActionX = decompressedReader.ReadInt16();
                ActionY = decompressedReader.ReadInt16();
                if (!Settings.Old)
                    Transparent = decompressedReader.ReadColor();
                else
                    Transparent = Color.Black; //ig?


                if (Settings.Android)
                {
                    //couldn't care less
                }
                else
                {
                    if (Settings.TwoFivePlus)
                    {
                        var decompSizePlus = decompressedReader.ReadInt32();
                        var rawImg = decompressedReader.ReadBytes(dataSize - 4);
                        var target = new byte[decompSizePlus];
                        LZ4Codec.Decode(rawImg, target);
                        imageData = target;
                    }
                    else if (Flags["LZX"])
                    {
                        var decompSize = decompressedReader.ReadInt32();
                        imageData = Decompressor.DecompressBlock(decompressedReader,
                            (int)(decompressedReader.Size() - decompressedReader.Tell()));
                    }
                    else
                    {
                        imageData = decompressedReader.ReadBytes(dataSize);
                    }
                }

                newImageData = null;
            });
            ImageBank.imageReadingTasks.Add(mainRead);
            if (!IsMFA && !Settings.Old && !Settings.TwoFivePlus)
                mainRead.Start();
            else mainRead.RunSynchronously();
        }

        public int WriteNew(ByteWriter writer)
        {
            PrepareForMfa();
            var start = writer.Tell();

            byte[] compressedImg = null;
            Flags["LZX"] = true;

            compressedImg = Decompressor.CompressBlock(imageData);

            writer.WriteInt32(Handle);
            writer.WriteInt32(Checksum);
            writer.WriteInt32(references);
            writer.WriteUInt32((uint)compressedImg.Length + 4);
            writer.WriteInt16((short)Width);
            writer.WriteInt16((short)Height);
            writer.WriteInt8(GraphicMode);
            writer.WriteInt8((byte)Flags.flag);
            writer.WriteInt16(0);
            writer.WriteInt16(HotspotX);
            writer.WriteInt16(HotspotY);
            writer.WriteInt16(ActionX);
            writer.WriteInt16(ActionY);
            writer.WriteColor(Transparent);
            writer.WriteInt32(imageData.Length);
            writer.WriteBytes(compressedImg);

            var chunkSize = 36 + compressedImg.Length;
            return (int)(chunkSize + 4 + start);
        }

        // CCN writer: serializes the image in its current graphic mode without the MFA
        // conversion done by WriteNew. Layout matches FusionImage.Read for normal CCN images.
        public void WriteCcn(ByteWriter writer)
        {
            byte[] compressedImg = Decompressor.CompressBlock(imageData);

            writer.WriteInt32(Handle);
            writer.WriteInt32(Checksum);
            writer.WriteInt32(references);
            writer.WriteUInt32((uint)compressedImg.Length + 4);
            writer.WriteInt16((short)Width);
            writer.WriteInt16((short)Height);
            writer.WriteInt8(GraphicMode);
            writer.WriteInt8((byte)Flags.flag);
            writer.WriteInt16(0);
            writer.WriteInt16(HotspotX);
            writer.WriteInt16(HotspotY);
            writer.WriteInt16(ActionX);
            writer.WriteInt16(ActionY);
            writer.WriteColor(Transparent);
            writer.WriteInt32(imageData.Length);
            writer.WriteBytes(compressedImg);
        }

        public override void Write(ByteWriter writer)
        {
        }

        public void PrepareForMfa()
        {
            switch (GraphicMode)
            {
                case 0:
                    imageData = ImageTranslator.AndroidMode0ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                    imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                    GraphicMode = 4;
                    break;
                case 1:
                    imageData = ImageTranslator.AndroidMode1ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                    imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                    GraphicMode = 4;
                    break;
                case 2:
                    imageData = ImageTranslator.AndroidMode2ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                    imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                    GraphicMode = 4;
                    break;
                case 3:
                    imageData = ImageTranslator.AndroidMode3ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                    imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                    GraphicMode = 4;
                    break;
                case 4:
                    if (Settings.Android)
                    {
                        imageData = ImageTranslator.AndroidMode4ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                        imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                        GraphicMode = 4;
                    }
                    else if (Settings.F3)
                    {
                        imageData = ImageTranslator.Normal24BitMaskedToRGBA(imageData, Width, Height, Flags["Alpha"], Transparent, Settings.Fusion3Seed);
                        imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                        GraphicMode = 4;
                    }
                    break;
                case 5:
                    imageData = ImageTranslator.AndroidMode5ToRGBA(imageData, Width, Height, Flags["Alpha"]);
                    imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"]);
                    GraphicMode = 4;
                    break;
                case 8:
                    imageData = ImageTranslator.TwoFivePlusToRGBA(imageData, Width, Height, Flags["Alpha"], Transparent, Flags["RGBA"], Settings.Fusion3Seed);
                    imageData = ImageTranslator.RGBAToRGBMasked(imageData, Width, Height, Flags["Alpha"], Transparent, Flags["RGBA"]);
                    GraphicMode = 4;
                    break;
            }
        }
    }
}
