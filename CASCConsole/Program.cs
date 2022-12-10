using CASCLib;
using System;
using System.CommandLine;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CASCConsole
{
    enum ExtractMode
    {
        Pattern,
        Listfile
    }

    class Program
    {
        static readonly object ProgressLock = new object();

        static void Main(string[] args)
        {
            var modeOption = new Option<ExtractMode>(new[] { "-m", "--mode" }, "Extraction mode") { IsRequired = true };
            var modeParamOption = new Option<string>(new[] { "-e", "--eparam" }, "Extraction mode parameter (example: *.* or listfile.csv)") { IsRequired = true };
            var destOption = new Option<string>(new[] { "-d", "--dest" }, "Destination folder path") { IsRequired = true };
            var localeOption = new Option<LocaleFlags>(new[] { "-l", "--locale" }, "Product locale") { IsRequired = true };
            var productOption = new Option<string>(new[] { "-p", "--product" }, "Product uid") { IsRequired = true };
            var onlineOption = new Option<bool>(new[] { "-o", "--online" }, () => false, "Online extraction mode");
            var storagePathOption = new Option<string>(new[] { "-s", "--storage" }, () => "", "Local game storage folder");
            var overrideArchiveOption = new Option<bool>(new[] { "-a", "--archive" }, () => false, "Override archive");

            var allOptions = new Option[] { modeOption, modeParamOption, destOption, localeOption, productOption, onlineOption, storagePathOption, overrideArchiveOption };

            var rootCommand = new RootCommand("CASCConsole");
            foreach (var option in allOptions)
                rootCommand.Add(option);

            rootCommand.SetHandler((context) =>
            {
                T get<T>(Option<T> option) => context.ParseResult.GetValueForOption(option);
                Extract(get(modeOption), get(modeParamOption), get(destOption), get(localeOption), get(productOption), get(onlineOption), get(storagePathOption), get(overrideArchiveOption));
            });
            rootCommand.Invoke(args);
        }

        private static void Extract(ExtractMode mode, string modeParam, string destFolder, LocaleFlags locale, string product, bool online, string storagePath, bool overrideArchive)
        {
            DateTime startTime = DateTime.Now;

            Console.WriteLine($"Started at {startTime}");

            Console.WriteLine("Settings:");
            Console.WriteLine("  Storage Path: {0}", storagePath);

            Console.WriteLine("Loading...");

            BackgroundWorkerEx bgLoader = new BackgroundWorkerEx();
            bgLoader.ProgressChanged += BgLoader_ProgressChanged;

            CASCConfig.LoadFlags |= LoadFlags.Install;

            CASCConfig config = online
                ? CASCConfig.LoadOnlineStorageConfig(product, "us")
                : CASCConfig.LoadLocalStorageConfig(storagePath, product);

            CASCHandler cascHandler = CASCHandler.OpenStorage(config, bgLoader);

            cascHandler.Root.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.csv"), bgLoader);
            CASCFolder root = cascHandler.Root.SetFlags(locale, overrideArchive);
            cascHandler.Root.MergeInstall(cascHandler.Install);

            Console.WriteLine("Loaded.");

            Console.WriteLine("Extract params:");
            Console.WriteLine("  Mode: {0}", mode);
            Console.WriteLine("  Mode Param: {0}", modeParam);
            Console.WriteLine("  Destination: {0}", destFolder);
            Console.WriteLine("  LocaleFlags: {0}", locale);
            Console.WriteLine("  Product: {0}", product);
            Console.WriteLine("  Online: {0}", online);
            Console.WriteLine("  OverrideArchive: {0}", overrideArchive);

            if (mode == ExtractMode.Pattern)
            {
                Wildcard wildcard = new Wildcard(modeParam, true, RegexOptions.IgnoreCase);

                foreach (var file in CASCFolder.GetFiles(root.Folders.Select(kv => kv.Value as ICASCEntry).Concat(root.Files.Select(kv => kv.Value))))
                {
                    if (wildcard.IsMatch(file.FullName))
                        ExtractFile(cascHandler, file.Hash, file.FullName, destFolder);
                }
            }
            else if (mode == ExtractMode.Listfile)
            {
                if (cascHandler.Root is WowRootHandler wowRoot)
                {
                    char[] splitChar = new char[] { ';' };

                    var names = File.ReadLines(modeParam).Select(s => s.Split(splitChar, 2)).Select(s => new { id = int.Parse(s[0]), name = s[1] });

                    foreach (var file in names)
                        ExtractFile(cascHandler, wowRoot.GetHashByFileDataId(file.id), file.name, destFolder);
                }
                else
                {
                    var names = File.ReadLines(modeParam);

                    foreach (var file in names)
                        ExtractFile(cascHandler, 0, file, destFolder);
                }
            }

            Console.WriteLine("Extracted.");

            DateTime endTime = DateTime.Now;
            Console.WriteLine($"Ended at {endTime} (took {endTime - startTime})");
        }

        private static void ExtractFile(CASCHandler cascHandler, ulong hash, string file, string dest)
        {
            Console.Write("Extracting '{0}'...", file);

            try
            {
                if (hash != 0)
                    cascHandler.SaveFileTo(hash, dest, file);
                else
                    cascHandler.SaveFileTo(file, dest);

                Console.WriteLine(" Ok!");
            }
            catch (Exception exc)
            {
                Console.WriteLine($" Error ({exc.Message})!");
                Logger.WriteLine(exc.Message);
            }
        }

        private static void BgLoader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lock (ProgressLock)
            {
                if (e.UserState != null)
                    Console.WriteLine(e.UserState);

                DrawProgressBar(e.ProgressPercentage, 100, 72, '#');
            }
        }

        private static void DrawProgressBar(long complete, long maxVal, int barSize, char progressCharacter)
        {
            float perc = (float)complete / maxVal;
            DrawProgressBar(perc, barSize, progressCharacter);
        }

        private static void DrawProgressBar(float percent, int barSize, char progressCharacter)
        {
            Console.CursorVisible = false;
            int left = Console.CursorLeft;
            int chars = (int)Math.Round(percent / (1.0f / barSize));
            string p1 = string.Empty, p2 = string.Empty;

            for (int i = 0; i < chars; i++)
                p1 += progressCharacter;
            for (int i = 0; i < barSize - chars; i++)
                p2 += progressCharacter;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(p1);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(p2);

            Console.ResetColor();
            Console.Write(" {0}%", (percent * 100).ToString("N2"));
            Console.CursorLeft = left;
        }
    }
}
