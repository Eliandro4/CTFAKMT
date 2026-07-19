using CTFAK.CCN;
using CTFAK.Core.CCN.Chunks.Banks.SoundBank;
using CTFAK.FileReaders;
using CTFAK.Memory;
using CTFAK.Utils;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ImageMagick;

namespace CTFAK.FileReaders
{
    public class ApkFileReader
    {
        public static SoundBank androidSoundBank = new();
        public static Dictionary<int, MagickImage> androidIcons = new();

        public static string ExtractCCN(string apkPath)
        {
            Settings.gameType = Settings.GameType.ANDROID;
            Directory.CreateDirectory(Path.GetTempPath() + "CTFAK/AndroidSounds");
            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.Name == "application.ccn")
                    {
                        entry.ExtractToFile(Path.GetTempPath() + "application.ccn");
                    }
                    else if (Path.GetExtension(entry.Name) == ".mp3" || 
                             Path.GetExtension(entry.Name) == ".ogg" || 
                             Path.GetExtension(entry.Name) == ".wav")
                    {
                        Stream soundBytes = entry.Open();
                        SoundItem Sound = new SoundItem();
                        Sound.AndroidRead(new ByteReader(soundBytes), entry.Name);
                        androidSoundBank.Items.Add(Sound);
                    }
                    else if (entry.FullName == "res/drawable-xhdpi/launcher.png")
                    {
                        using var entryStream = entry.Open();
                        using var ms = new MemoryStream();
                        entryStream.CopyTo(ms);
                        var iconData = ms.ToArray();
                        
                        var icon16 = new MagickImage(iconData); icon16.Resize(16, 16); androidIcons[16] = icon16;
                        var icon17 = new MagickImage(iconData); icon17.Resize(16, 16); androidIcons[17] = icon17;
                        var icon32 = new MagickImage(iconData); icon32.Resize(32, 32); androidIcons[32] = icon32;
                        var icon33 = new MagickImage(iconData); icon33.Resize(32, 32); androidIcons[33] = icon33;
                        var icon48 = new MagickImage(iconData); icon48.Resize(48, 48); androidIcons[48] = icon48;
                        var icon49 = new MagickImage(iconData); icon49.Resize(48, 48); androidIcons[49] = icon49;
                        var icon128 = new MagickImage(iconData); icon128.Resize(128, 128); androidIcons[128] = icon128;
                        var icon256 = new MagickImage(iconData); icon256.Resize(256, 256); androidIcons[256] = icon256;
                    }
                }
            }
            if (File.Exists(Path.GetTempPath() + "application.ccn"))
                return Path.GetTempPath() + "application.ccn";
            else
                return apkPath;
        }
    }
}
