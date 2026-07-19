using System;
using CTFAK.FileReaders;
using CTFAK.Utils;

namespace CTFAK
{
    public class CTFAKCore
    {
        public delegate void SaveHandler(int index, int all);

        public static IFileReader currentReader;
        public static string parameters;
        public static string path;
        public static void Init()
        {

            AppDomain.CurrentDomain.UnhandledException += (o, e) =>
            {
                Logger.Log(e.ExceptionObject.ToString(), true, ConsoleColor.Red);
            };
        }
    }
}
