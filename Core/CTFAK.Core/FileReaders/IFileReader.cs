using CTFAK.CCN;
using CTFAK.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;

namespace CTFAK.FileReaders
{
    public interface IFileReader
    {
        string Name { get; }

        GameData getGameData();
        void LoadGame(string gamePath);
        Dictionary<int, MagickImage> getIcons();
        void PatchMethods();
        IFileReader Copy();

    }
}
