using CTFAK.Memory;
using System;
using System.IO;
using ImageMagick;

namespace CTFAK.Utils
{
    public static class Utils
    {
        public static string ClearName(string ogName)
        {
            var str = string.Join("", ogName.Split(Path.GetInvalidFileNameChars()));
            str = str.Replace("?", "");
            return str;
        }
        public static string ReadUniversal(this ByteReader reader, int len=-1)
        {
            if (Settings.Unicode) return reader.ReadWideString(len);
            else return reader.ReadAscii(len);
        }
        public static byte[] GetBuffer(this ByteWriter writer)
        {
            var buf = writer.ToArray();
            Array.Resize(ref buf, (int)writer.Size());
            return buf;
        }
        public static MagickImage ResizeImage(this MagickImage imgToResize, int size) => ResizeImage(imgToResize, size, size);
        public static MagickImage ResizeImage(this MagickImage imgToResize, int width, int height) => ResizeImage(imgToResize, new Size(width, height));
        public static MagickImage ResizeImage(this MagickImage imgToResize, Size size)
        {
            var resized = (MagickImage)imgToResize.Clone();
            resized.Resize((uint)size.Width, (uint)size.Height);
            return resized;
        }
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
        public static string GetHex(this byte[] data, int count = -1, int position = 0)
        {
            var actualCount = count;
            if (actualCount == -1) actualCount = data.Length;
            string temp = "";
            for (int i = 0; i < actualCount; i++)
            {
                temp += data[i].ToString("X2");
                temp += " ";
            }
            return temp;
        }
        public static T[] To1DArray<T>(T[,] input)
        {
            int size = input.Length;
            T[] result = new T[size];

            int write = 0;
            for (int i = 0; i <= input.GetUpperBound(0); i++)
            {
                for (int z = 0; z <= input.GetUpperBound(1); z++)
                {
                    result[write++] = input[i, z];
                }
            }

            return result;
        }
    }
}
