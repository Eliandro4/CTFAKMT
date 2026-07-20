using CTFAK.CCN;
using CTFAK.Core.CCN.Chunks.Banks.ImageBank;
using CTFAK.Core.CCN.Chunks.Banks.SoundBank;
using CTFAK.CCN.Chunks.Frame;
using CTFAK.MMFParser.EXE.Loaders.Events.Parameters;
using CTFAK.MMFParser.EXE.Loaders.Events.Expressions;
using CTFAK.CCN.Chunks.Objects;
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
            DumpAllStrings(dir);
        }

        public void DumpAllStrings(string dir)
        {
            dir = Path.Combine(dir, "strings");
            Directory.CreateDirectory(dir);
            var lines = new List<string>();
            var index = 0;

            void Add(string key, string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                lines.Add($"{key}={Escape(value)}");
                index++;
            }

            if (Data.globalStrings?.Items != null)
            {
                for (int i = 0; i < Data.globalStrings.Items.Count; i++)
                    Add($"GLOBAL:{i}", Data.globalStrings.Items[i]);
            }

            Add("APP:NAME", Data.name);
            Add("APP:AUTHOR", Data.author);
            Add("APP:COPYRIGHT", Data.copyright);

            if (Data.frames != null)
            {
                for (int f = 0; f < Data.frames.Count; f++)
                {
                    var frame = Data.frames[f];
                    Add($"FRAME:{f}:NAME", frame.name);

                    if (frame.objects != null)
                    {
                        for (int o = 0; o < frame.objects.Count; o++)
                        {
                            var objInst = frame.objects[o];
                            if (!Data.frameitems.TryGetValue(objInst.objectInfo, out var obj)) continue;
                            Add($"OBJECT:{obj.handle}:NAME", obj.name);

                            if (obj.properties is Text text)
                            {
                                for (int p = 0; p < text.Items.Count; p++)
                                    Add($"TEXT:{obj.handle}:{p}", text.Items[p].Value);
                            }

                            if (obj.properties is ObjectCommon common)
                            {
                                if (common.Strings?.Items != null)
                                {
                                    for (int s = 0; s < common.Strings.Items.Count; s++)
                                        Add($"ALTSTR:{obj.handle}:{s}", common.Strings.Items[s]);
                                }
                            }
                        }
                    }

                    if (frame.events != null && frame.events.Items != null)
                    {
                        for (int eg = 0; eg < frame.events.Items.Count; eg++)
                        {
                            var egItem = frame.events.Items[eg];
                            for (int c = 0; c < egItem.Conditions.Count; c++)
                            {
                                var cond = egItem.Conditions[c];
                                for (int p = 0; p < cond.Items.Count; p++)
                                {
                                    if (cond.Items[p].Loader is StringParam sp)
                                        Add($"EVENT:{f}:{eg}:COND:{c}:{p}", sp.Value);
                                    else if (cond.Items[p].Loader is ExpressionParameter ep)
                                    {
                                        foreach (var expr in ep.Items)
                                        {
                                            if (expr.Loader is StringExp se)
                                                Add($"EVENT:{f}:{eg}:COND:{c}:{p}:EXP", (string)se.Value);
                                        }
                                    }
                                }
                            }
                            for (int a = 0; a < egItem.Actions.Count; a++)
                            {
                                var act = egItem.Actions[a];
                                for (int p = 0; p < act.Items.Count; p++)
                                {
                                    if (act.Items[p].Loader is StringParam sp)
                                        Add($"EVENT:{f}:{eg}:ACT:{a}:{p}", sp.Value);
                                    else if (act.Items[p].Loader is ExpressionParameter ep)
                                    {
                                        foreach (var expr in ep.Items)
                                        {
                                            if (expr.Loader is StringExp se)
                                                Add($"EVENT:{f}:{eg}:ACT:{a}:{p}:EXP", (string)se.Value);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            File.WriteAllLines(Path.Combine(dir, "all_strings.txt"), lines);
            Log($"Dumped {index} strings to {dir}");
        }

        public void ImportStrings(string file)
        {
            if (!File.Exists(file))
            {
                Log($"Import file not found: {file}");
                return;
            }

            var updated = 0;
            foreach (var line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq);
                var value = Unescape(line.Substring(eq + 1));

                if (key.StartsWith("GLOBAL:", StringComparison.OrdinalIgnoreCase) && int.TryParse(key.Substring(7), out var gi))
                {
                    if (Data.globalStrings?.Items != null && gi >= 0 && gi < Data.globalStrings.Items.Count)
                    {
                        Data.globalStrings.Items[gi] = value;
                        Data.EditedChunks.Add(8755);
                        updated++;
                    }
                }
                else if (key.Equals("APP:NAME", StringComparison.OrdinalIgnoreCase))
                {
                    Data.name = value;
                    Data.EditedChunks.Add(8740);
                    updated++;
                }
                else if (key.Equals("APP:AUTHOR", StringComparison.OrdinalIgnoreCase))
                {
                    Data.author = value;
                    Data.EditedChunks.Add(8741);
                    updated++;
                }
                else if (key.Equals("APP:COPYRIGHT", StringComparison.OrdinalIgnoreCase))
                {
                    Data.copyright = value;
                    Data.EditedChunks.Add(8763);
                    updated++;
                }
                else if (key.StartsWith("OBJECT:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = key.Split(':');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var oh) && parts[2] == "NAME")
                    {
                        var obj = Data.frameitems.Values.FirstOrDefault(o => o.handle == oh);
                        if (obj != null)
                        {
                            obj.name = value;
                            Data.EditedChunks.Add(8788);
                            updated++;
                        }
                    }
                }
                else if (key.StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = key.Split(':');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var th) && int.TryParse(parts[2], out var tp))
                    {
                        var obj = Data.frameitems.Values.FirstOrDefault(o => o.handle == th);
                        if (obj?.properties is Text text && tp >= 0 && tp < text.Items.Count)
                        {
                            text.Items[tp].Value = value;
                            Data.EditedChunks.Add(8790);
                            updated++;
                        }
                    }
                }
                else if (key.StartsWith("ALTSTR:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = key.Split(':');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var ah) && int.TryParse(parts[2], out var ai))
                    {
                        var obj = Data.frameitems.Values.FirstOrDefault(o => o.handle == ah);
                        if (obj?.properties is ObjectCommon common && common.Strings?.Items != null && ai >= 0 && ai < common.Strings.Items.Count)
                        {
                            common.Strings.Items[ai] = value;
                            Data.EditedChunks.Add(8790);
                            updated++;
                        }
                    }
                }
                else if (key.StartsWith("EVENT:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = key.Split(':');
                    if (parts.Length >= 6 && int.TryParse(parts[1], out var frameIdx)
                        && int.TryParse(parts[2], out var egIdx)
                        && int.TryParse(parts[4], out var condActIdx)
                        && int.TryParse(parts[5], out var paramIdx))
                    {
                        if (frameIdx < 0 || frameIdx >= Data.frames.Count)
                        {
                            Log($"Import skipped for event key '{key}': frame index {frameIdx} out of range.");
                            continue;
                        }

                        var frame = Data.frames[frameIdx];
                        if (frame.events == null || egIdx < 0 || egIdx >= frame.events.Items.Count)
                        {
                            Log($"Import skipped for event key '{key}': event group {egIdx} not found.");
                            continue;
                        }

                        var eg = frame.events.Items[egIdx];
                        bool isCond = parts[3].Equals("COND", StringComparison.OrdinalIgnoreCase);
                        bool isAct = parts[3].Equals("ACT", StringComparison.OrdinalIgnoreCase);
                        if (!isCond && !isAct)
                        {
                            Log($"Import skipped for event key '{key}': unknown type '{parts[3]}'.");
                            continue;
                        }

                        List<Parameter> paramList = null;
                        if (isCond)
                        {
                            if (condActIdx < 0 || condActIdx >= eg.Conditions.Count)
                            {
                                Log($"Import skipped for event key '{key}': condition index {condActIdx} out of range.");
                                continue;
                            }
                            paramList = eg.Conditions[condActIdx].Items;
                        }
                        else
                        {
                            if (condActIdx < 0 || condActIdx >= eg.Actions.Count)
                            {
                                Log($"Import skipped for event key '{key}': action index {condActIdx} out of range.");
                                continue;
                            }
                            paramList = eg.Actions[condActIdx].Items;
                        }

                        if (paramIdx < 0 || paramIdx >= paramList.Count)
                        {
                            Log($"Import skipped for event key '{key}': parameter index {paramIdx} out of range.");
                            continue;
                        }
                        bool isExp = parts.Length >= 7 && parts[5].Equals("EXP", StringComparison.OrdinalIgnoreCase);

                        if (isExp)
                        {
                            if (paramList[paramIdx].Loader is ExpressionParameter ep)
                            {
                                bool found = false;
                                foreach (var expr in ep.Items)
                                {
                                    if (expr.Loader is StringExp se)
                                    {
                                        se.Value = value;
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    Log($"Import skipped for event key '{key}': no StringExp found in expression parameter.");
                                    continue;
                                }
                            }
                            else
                            {
                                Log($"Import skipped for event key '{key}': parameter is not an expression.");
                                continue;
                            }
                        }
                        else
                        {
                            if (paramList[paramIdx].Loader is StringParam sp)
                            {
                                sp.Value = value;
                            }
                            else
                            {
                                Log($"Import skipped for event key '{key}': parameter is not a StringParam.");
                                continue;
                            }
                        }

                        frame.EventsModified = true;
                        Data.ModifiedFrameIndices.Add(frameIdx);
                        Data.EditedChunks.Add(13107);
                        updated++;
                    }
                    else
                    {
                        Log($"Import skipped for malformed event key '{key}'.");
                    }
                }
                else
                {
                    Log($"Import skipped for unknown key '{key}'.");
                }
            }

            Log($"Imported {updated} strings from {file}");
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("=", "\\=");
        }

        private static string Unescape(string value)
        {
            return value.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\=", "=").Replace("\\\\", "\\");
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
