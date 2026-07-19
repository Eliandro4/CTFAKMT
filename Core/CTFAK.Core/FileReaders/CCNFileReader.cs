using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CTFAK.CCN;
using CTFAK.CCN.Chunks.Banks;
using CTFAK.FileReaders;
using CTFAK.Memory;
using CTFAK.Utils;
using ImageMagick;

namespace CTFAK.EXE
{
    public class CCNFileReader:IFileReader
    {
        public string Name => "CCN";
        public GameData game;
        public GameData getGameData()
        {
            return game;
        }

        public void LoadGame(string gamePath)
        {
            var reader = new ByteReader(gamePath, System.IO.FileMode.Open);

            byte[] prefix = null;
            if (reader.PeekInt32() == 2004318071)
            {
                reader.Seek(0);
                prefix = reader.ReadBytes(32);
            }

            game = new GameData();
            game.FilePrefix = prefix;
            game.Read(reader);
        }

        public Dictionary<int, MagickImage> getIcons()
        {
            return ApkFileReader.androidIcons;
        }

        public void PatchMethods()
        {
            //Settings.gameType = Settings.GameType.ANDROID;
        }

        public IFileReader Copy()
        {
            CCNFileReader reader = new CCNFileReader();
            reader.game = game;
            return reader;
        }
    }
}