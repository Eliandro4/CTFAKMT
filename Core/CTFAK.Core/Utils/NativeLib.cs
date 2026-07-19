using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CTFAK.Utils
{
    public static class NativeLib
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static int decompressOld(IntPtr source, int source_size, IntPtr output, int output_size)
        {
            var input = new byte[source_size];
            Marshal.Copy(source, input, 0, source_size);
            var outputBuffer = new byte[output_size];
            int actualSize;

            using (var ms = new MemoryStream(input))
            using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
            {
                actualSize = deflate.Read(outputBuffer, 0, output_size);
            }

            Marshal.Copy(outputBuffer, 0, output, actualSize);
            return actualSize;
        }

        public static void TranslateToRGBMasked(IntPtr result, int width, int height, int alpha, int size, IntPtr imageData, int tranparent, int colorMode)
        {
        }

        public static void TranslateToRGBA(IntPtr result, int width, int height, int alpha, int size, IntPtr imageData, int tranparent, int colorMode)
        {
        }

        public static void TranslateToBGRA(IntPtr result, int width, int height, int alpha, int size, IntPtr imageData, int tranparent, int colorMode)
        {
        }
    }
}
