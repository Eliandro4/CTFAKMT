using CTFAK.CCN;
using CTFAK.Core.CCN.Chunks.Banks.ImageBank;
using CTFAK.Core.CCN.Chunks.Banks.SoundBank;
using CTFAK.Utils;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CTFAKModTool
{
    // Shared state exposed to mod scripts. Mirrors the role of UTMT's script globals.
    public static class ModGlobals
    {
        public static GameData Data;
        public static string OutputDirectory;
        public static bool Verbose;

        public static void Log(string message)
        {
            Logger.Log(message);
        }
    }

    // Scripting surface over a loaded GameData. Scripts get `Data` in scope plus these helpers,
    // loosely modeled on UndertaleModTool's IScriptInterface.
    public class ModScriptInterface
    {
        public GameData Data => ModGlobals.Data;

        public string AppName => Data?.name;
        public string Author => Data?.author;
        public int FusionBuild => Data?.productBuild ?? 0;

        public int ImageCount => Data?.Images?.Items?.Count ?? 0;
        public int SoundCount => Data?.Sounds?.Items?.Count ?? 0;
        public int FontCount => Data?.Fonts?.Items?.Count ?? 0;
        public int StringCount => Data?.globalStrings?.Items?.Count ?? 0;

        public void Log(string message) => ModGlobals.Log(message);

        public void DumpAll(string dir)
        {
            DumpImages(dir);
            DumpSounds(dir);
            DumpStrings(dir);
        }

        public void DumpImages(string dir)
        {
            dir = Path.Combine(dir, "images");
            Directory.CreateDirectory(dir);
            foreach (var img in Data.Images.Items.Values)
            {
                try
                {
                    var bmp = img.bitmap;
                    if (bmp == null) continue;
                    var name = $"img_{img.Handle}";
                    bmp.Write(Path.Combine(dir, $"{name}.png"), MagickFormat.Png);
                }
                catch (Exception e)
                {
                    Log($"Failed to dump image {img.Handle}: {e.Message}");
                }
            }
            Log($"Dumped {Data.Images.Items.Count} images to {dir}");
        }

        public void DumpSounds(string dir)
        {
            dir = Path.Combine(dir, "sounds");
            Directory.CreateDirectory(dir);
            foreach (var snd in Data.Sounds.Items)
            {
                try
                {
                    var name = Sanitize(snd.Name) ?? $"snd_{snd.Handle}";
                    var ext = snd.Flags == 33 ? ".wav" : ".bin";
                    File.WriteAllBytes(Path.Combine(dir, $"{name}{ext}"), snd.Data);
                }
                catch (Exception e)
                {
                    Log($"Failed to dump sound {snd.Handle}: {e.Message}");
                }
            }
            Log($"Dumped {Data.Sounds.Items.Count} sounds to {dir}");
        }

        public void DumpStrings(string dir)
        {
            dir = Path.Combine(dir, "strings");
            Directory.CreateDirectory(dir);
            var lines = new List<string>();
            if (Data.globalStrings?.Items != null)
            {
                for (int i = 0; i < Data.globalStrings.Items.Count; i++)
                    lines.Add($"[{i}] {Data.globalStrings.Items[i]}");
            }
            File.WriteAllLines(Path.Combine(dir, "globalStrings.txt"), lines);
            Log($"Dumped {lines.Count} global strings to {dir}");
        }

        // Replace an image by handle with a PNG file on disk.
        public void ReplaceImage(int handle, string pngPath)
        {
            if (!Data.Images.Items.TryGetValue(handle, out var img))
            {
                Log($"ReplaceImage: no image with handle {handle}");
                return;
            }
            using var magick = new MagickImage(pngPath);
            magick.Alpha(AlphaOption.On);
            img.FromBitmap(magick);
            img.Handle = handle;
            Data.EditedChunks.Add(26214);
            Log($"Replaced image {handle} ({img.Width}x{img.Height})");
        }

        // Replace an image identified by handle (as string). Images have no name field.
        public void ReplaceImage(string identifier, string pngPath)
        {
            if (int.TryParse(identifier, out var handle))
            {
                ReplaceImage(handle, pngPath);
                return;
            }
            Log($"ReplaceImage: images have no name field; use a numeric handle, not '{identifier}'.");
        }

        public void ReplaceSound(int handle, string filePath)
        {
            var snd = Data.Sounds.Items.FirstOrDefault(s => s.Handle == handle);
            if (snd == null)
            {
                Log($"ReplaceSound: no sound with handle {handle}");
                return;
            }
            snd.Data = File.ReadAllBytes(filePath);
            snd.Name = Path.GetFileNameWithoutExtension(filePath);
            Data.EditedChunks.Add(26216);
            Log($"Replaced sound {handle}");
        }

        // Replace a global string by index.
        public void ReplaceString(int index, string value)
        {
            if (Data.globalStrings?.Items == null || index < 0 || index >= Data.globalStrings.Items.Count)
            {
                Log($"ReplaceString: index {index} out of range");
                return;
            }
            Data.globalStrings.Items[index] = value;
            Data.EditedChunks.Add(8755);
            Log($"Replaced global string [{index}]");
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
