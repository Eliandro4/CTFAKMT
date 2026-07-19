using System;
using System.Collections.Generic;
using System.IO;
using ImageMagick;

namespace CTFAK.Utils
{
    public static class IconUtil
    {
        public static byte[] ToBytes(MagickImage icon)
        {
            using var ms = new MemoryStream();
            icon.Write(ms, MagickFormat.Png);
            return ms.ToArray();
        }

        public static MagickImage ToMagickImage(byte[] iconBytes)
        {
            using var ms = new MemoryStream(iconBytes);
            return new MagickImage(ms);
        }

        public static int GetBitCount(MagickImage icon)
        {
            return 32;
        }

        public static List<MagickImage> Split(MagickImage icon)
        {
            return new List<MagickImage> { icon };
        }
    }
}
