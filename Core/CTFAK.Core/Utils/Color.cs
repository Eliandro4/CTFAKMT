namespace CTFAK.Utils
{
    public struct Color
    {
        public byte A;
        public byte R;
        public byte G;
        public byte B;

        public Color(byte r, byte g, byte b)
        {
            A = 255;
            R = r;
            G = g;
            B = b;
        }

        public Color(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static Color FromArgb(int a, int r, int g, int b)
        {
            return new Color((byte)a, (byte)r, (byte)g, (byte)b);
        }

        public static Color FromArgb(int r, int g, int b)
        {
            return new Color((byte)r, (byte)g, (byte)b);
        }

        public static Color FromArgb(int a, Color baseColor)
        {
            return new Color((byte)a, baseColor.R, baseColor.G, baseColor.B);
        }

        public static Color Black => new Color(0, 0, 0);
        public static Color White => new Color(255, 255, 255);
        public static Color Transparent => new Color(0, 0, 0, 0);
        public static Color Brown => new Color(165, 42, 42);
    }

    public struct Size
    {
        public int Width;
        public int Height;

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
