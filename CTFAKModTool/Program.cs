using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CTFAK;
using CTFAK.CCN;
using CTFAK.Core.CCN.Chunks.Banks.ImageBank;
using CTFAK.EXE;
using CTFAK.Memory;
using CTFAKModTool;
using CTFAK.Utils;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace CTFAKModTool.Cli
{
    public class Program
    {
        public static int Main(string[] args)
        {
            CTFAKCore.Init();

            var fileArgument = new Argument<FileInfo>("file")
            {
                Description = "Path to the game data file (.ccn, .dat, .mfa)"
            };

            // info
            var infoCommand = new Command("info", "Show basic info about the game data file")
            {
                fileArgument
            };
            infoCommand.SetHandler(ctx => Info(ctx.ParseResult.GetValueForArgument(fileArgument)));

            // dump
            var dumpOutput = new Option<DirectoryInfo>("-o", "--output")
            {
                Description = "Where to dump data. Defaults to a folder next to the file."
            };
            var dumpImages = new Option<bool>("-i", "--images") { Description = "Dump all images as PNG." };
            var dumpSounds = new Option<bool>("-s", "--sounds") { Description = "Dump all sounds." };
            var dumpStrings = new Option<bool>("--strings") { Description = "Dump all global strings." };
            var dumpCommand = new Command("dump", "Dump properties of the game data file")
            {
                fileArgument, dumpOutput, dumpImages, dumpSounds, dumpStrings
            };
            dumpCommand.SetHandler(ctx => Dump(
                ctx.ParseResult.GetValueForArgument(fileArgument),
                ctx.ParseResult.GetValueForOption(dumpOutput),
                ctx.ParseResult.GetValueForOption(dumpImages),
                ctx.ParseResult.GetValueForOption(dumpSounds),
                ctx.ParseResult.GetValueForOption(dumpStrings)));

            // script / load
            var scriptOpt = new Option<FileInfo[]>("-s", "--script")
            {
                Description = "C# script file(s) to run against the loaded data."
            };
            var outOpt = new Option<FileInfo>("-o", "--output")
            {
                Description = "Where to save the modified data file (load/save)."
            };
            var loadCommand = new Command("load", "Load a data file, run script(s), optionally save")
            {
                fileArgument, scriptOpt, outOpt
            };
            loadCommand.SetHandler(ctx => Load(
                ctx.ParseResult.GetValueForArgument(fileArgument),
                ctx.ParseResult.GetValueForOption(scriptOpt),
                ctx.ParseResult.GetValueForOption(outOpt)));

            var scriptCommand = new Command("script", "Alias of 'load' without saving")
            {
                fileArgument, scriptOpt
            };
            scriptCommand.SetHandler(ctx => Load(
                ctx.ParseResult.GetValueForArgument(fileArgument),
                ctx.ParseResult.GetValueForOption(scriptOpt),
                null));

            // replace
            var replaceImages = new Option<string[]>("-i", "--images")
            {
                Description = "Image replacements, e.g. '12=./new.png'."
            };
            var replaceCommand = new Command("replace", "Replace entries in the game data file")
            {
                fileArgument, outOpt, replaceImages
            };
            replaceCommand.SetHandler(ctx => Replace(
                ctx.ParseResult.GetValueForArgument(fileArgument),
                ctx.ParseResult.GetValueForOption(outOpt),
                ctx.ParseResult.GetValueForOption(replaceImages)));

            var importStringsOpt = new Option<FileInfo>("-i", "--input")
            {
                Description = "Strings file to import (key=value format)."
            };
            var importCommand = new Command("import", "Import strings back into the game data file")
            {
                fileArgument, outOpt, importStringsOpt
            };
            importCommand.SetHandler(ctx => Import(
                ctx.ParseResult.GetValueForArgument(fileArgument),
                ctx.ParseResult.GetValueForOption(outOpt),
                ctx.ParseResult.GetValueForOption(importStringsOpt)));

            var root = new RootCommand("CTFAKMT modding CLI — load, inspect, edit and re-save Clickteam Fusion games.")
            {
                infoCommand, dumpCommand, loadCommand, scriptCommand, replaceCommand, importCommand
            };

            return root.Invoke(args);
        }

        private static GameData LoadGame(FileInfo file)
        {
            CTFAKCore.path = file.FullName;
            CTFAKCore.parameters = "";
            var reader = new CCNFileReader();
            reader.LoadGame(file.FullName);
            var game = reader.getGameData();
            if (game == null)
                throw new Exception("Failed to read game data.");
            return game;
        }

        private static int Info(FileInfo file)
        {
            if (!file.Exists) { Console.WriteLine("File not found: " + file.FullName); return 1; }
            var game = LoadGame(file);
            Console.WriteLine("=== Game Info ===");
            Console.WriteLine($"Name:        {game.name}");
            Console.WriteLine($"Author:      {game.author}");
            Console.WriteLine($"Copyright:   {game.copyright}");
            Console.WriteLine($"Fusion Build:{game.productBuild}");
            Console.WriteLine($"Images:      {game.Images?.Items?.Count ?? 0}");
            Console.WriteLine($"Sounds:      {game.Sounds?.Items?.Count ?? 0}");
            Console.WriteLine($"Fonts:       {game.Fonts?.Items?.Count ?? 0}");
            Console.WriteLine($"GlobalStrings:{game.globalStrings?.Items?.Count ?? 0}");
            Console.WriteLine($"Frames:      {game.frames?.Count ?? 0}");
            Console.WriteLine($"Chunks read: {game.RawChunks.Count}");
            return 0;
        }

        private static int Dump(FileInfo file, DirectoryInfo output, bool images, bool sounds, bool strings)
        {
            if (!file.Exists) { Console.WriteLine("File not found: " + file.FullName); return 1; }
            var game = LoadGame(file);
            var dir = output?.FullName ?? Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.FullName) + "_dump");
            ModGlobals.Data = game;
            var api = new ModScriptInterface();
            if (!images && !sounds && !strings)
            {
                images = sounds = strings = true;
            }
            if (images) api.DumpImages(dir);
            if (sounds) api.DumpSounds(dir);
            if (strings) api.DumpStrings(dir);
            return 0;
        }

        private static async Task<int> Load(FileInfo file, FileInfo[] scripts, FileInfo output)
        {
            if (!file.Exists) { Console.WriteLine("File not found: " + file.FullName); return 1; }
            var game = LoadGame(file);
            ModGlobals.Data = game;
            var api = new ModScriptInterface();

            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    if (!script.Exists) { Console.WriteLine("Script not found: " + script.FullName); return 1; }
                    Console.WriteLine($"Running script: {script.Name}");
                    var code = await File.ReadAllTextAsync(script.FullName);
                    var options = ScriptOptions.Default
                        .WithImports("System", "System.IO", "ImageMagick", "CTFAK.CCN", "CTFAKModTool")
                        .WithReferences(typeof(ModScriptInterface).Assembly, typeof(GameData).Assembly);
                    var globals = new ScriptGlobals { Data = game, API = api };
                    try
                    {
                        await CSharpScript.RunAsync(code, options, globals);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Script error: " + e.Message);
                        return 1;
                    }
                }
            }

            if (output != null)
            {
                Save(game, output.FullName);
                Console.WriteLine($"Saved modified data to {output.FullName}");
            }
            else
            {
                Console.WriteLine("No -o/--output given; changes were not saved.");
            }
            return 0;
        }

        private static int Replace(FileInfo file, FileInfo output, string[] images)
        {
            if (!file.Exists) { Console.WriteLine("File not found: " + file.FullName); return 1; }
            if (output == null) { Console.WriteLine("An --output file is required for replace."); return 1; }
            var game = LoadGame(file);
            ModGlobals.Data = game;
            var api = new ModScriptInterface();

            if (images != null)
            {
                foreach (var spec in images)
                {
                    var parts = spec.Split('=', 2);
                    if (parts.Length != 2) { Console.WriteLine($"Bad image spec: {spec}"); continue; }
                    if (int.TryParse(parts[0], out var handle))
                        api.ReplaceImage(handle, parts[1]);
                    else
                        api.ReplaceImage(parts[0], parts[1]);
                }
            }

            Save(game, output.FullName);
            Console.WriteLine($"Saved modified data to {output.FullName}");
            return 0;
        }

        private static int Import(FileInfo file, FileInfo output, FileInfo stringsFile)
        {
            if (!file.Exists) { Console.WriteLine("File not found: " + file.FullName); return 1; }
            if (output == null) { Console.WriteLine("An --output file is required for import."); return 1; }
            if (stringsFile == null || !stringsFile.Exists) { Console.WriteLine("An --input strings file is required for import."); return 1; }
            var game = LoadGame(file);
            ModGlobals.Data = game;
            var api = new ModScriptInterface();

            api.ImportStrings(stringsFile.FullName);

            Save(game, output.FullName);
            Console.WriteLine($"Saved modified data to {output.FullName}");
            return 0;
        }

        private static void Save(GameData game, string path)
        {
            using var writer = new ByteWriter(path, FileMode.Create);
            game.Write(writer);
            writer.Flush();
        }
    }

    // Globals object available to scripts: `Data` and `API` are in scope.
    public class ScriptGlobals
    {
        public GameData Data;
        public ModScriptInterface API;
    }
}
