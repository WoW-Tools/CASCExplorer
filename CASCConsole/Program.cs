using CASCLib;
using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace CASCConsole
{
    enum ExtractMode
    {
        Pattern,
        Listfile
    }

    class CASCConsoleOptions
    {
        public ExtractMode Mode { get; set; }
        public string ModeParam { get; set; }
        public string Region { get; set; }
        public string DestFolder { get; set; }
        public LocaleFlags Locale { get; set; }
        public string Product { get; set; }
        public bool Online { get; set; }
        public string StoragePath { get; set; }
        public bool OverrideArchive { get; set; }
        public bool PreferHighResTextures { get; set; }
    }

    internal class CASCConsoleOptionsBinder : BinderBase<CASCConsoleOptions>
    {
        private readonly Option<ExtractMode> modeOption = new Option<ExtractMode>(new[] { "-m", "--mode" }, "Extraction mode") { IsRequired = true };
        private readonly Option<string> modeParamOption = new Option<string>(new[] { "-e", "--eparam" }, "Extraction mode parameter (example: *.* or listfile.csv)") { IsRequired = true };
        private readonly Option<string> region = new Option<string>(new[] { "-r", "--region" }, () => "us", "Region");
        private readonly Option<string> destOption = new Option<string>(new[] { "-d", "--dest" }, "Destination folder path") { IsRequired = true };
        private readonly Option<LocaleFlags> localeOption = new Option<LocaleFlags>(new[] { "-l", "--locale" }, "Product locale") { IsRequired = true };
        private readonly Option<string> productOption = new Option<string>(new[] { "-p", "--product" }, "Product uid") { IsRequired = true };
        private readonly Option<bool> onlineOption = new Option<bool>(new[] { "-o", "--online" }, () => false, "Online extraction mode");
        private readonly Option<string> storagePathOption = new Option<string>(new[] { "-s", "--storage" }, () => "", "Local game storage folder");
        private readonly Option<bool> overrideArchiveOption = new Option<bool>(new[] { "-a", "--archive" }, () => false, "Override archive");
        private readonly Option<bool> preferHighResTexturesOption = new Option<bool>(new[] { "-h", "--highres" }, () => false, "High Resolution Textures");

        public RootCommand Root { get; }

        public CASCConsoleOptionsBinder()
        {
            Root = new RootCommand("CASCConsole") { modeOption, modeParamOption, region, destOption, localeOption, productOption, onlineOption, storagePathOption, overrideArchiveOption, preferHighResTexturesOption };
        }

        protected override CASCConsoleOptions GetBoundValue(BindingContext bindingContext)
        {
            var parseResult = bindingContext.ParseResult;

            return new CASCConsoleOptions
            {
                Mode = parseResult.GetValueForOption(modeOption),
                ModeParam = parseResult.GetValueForOption(modeParamOption),
                Region = parseResult.GetValueForOption(region),
                DestFolder = parseResult.GetValueForOption(destOption),
                Locale = parseResult.GetValueForOption(localeOption),
                Product = parseResult.GetValueForOption(productOption),
                Online = parseResult.GetValueForOption(onlineOption),
                StoragePath = parseResult.GetValueForOption(storagePathOption),
                OverrideArchive = parseResult.GetValueForOption(overrideArchiveOption),
                PreferHighResTextures = parseResult.GetValueForOption(preferHighResTexturesOption),
            };
        }
    }

    class Program
    {
        static readonly object ProgressLock = new object();

        static void Main(string[] args)
        {
            //var files = Directory.GetFiles("f:\\wowdev\\");

            //ArmadilloCrypt crypt = new ArmadilloCrypt("fenrisdev");
            //var x = crypt.DecryptFile("f:\\fenrisdev\\tpr\\fenrisdev\\data\\a5\\fb\\a5fb39468b6de0d29ae5ddbd9b1deae6.index");
            //File.WriteAllBytes("a5fb39468b6de0d29ae5ddbd9b1deae6.index.dec", x);
            //;
            //var cdnCfg = KeyValueConfig.ReadKeyValueConfig(File.OpenText("c:\\Games\\World of Warcraft\\Data\\config\\b2\\26\\b226e6974a6289092a5fe51bf2cea5f9"));
            //var archives = cdnCfg["archives"];
            //var patcharchives = cdnCfg["patch-archives"];
            //var fileIndex = cdnCfg["file-index"];
            //var patchFileIndex = cdnCfg["patch-file-index"];

            //var configs = new string[] { "4847cc7eb35c2f387a06e4d0321ddf53", "b226e6974a6289092a5fe51bf2cea5f9", "a473b90a54795b5be9fe207aacd1a000" };

            //WebClient client = new WebClient();

            //string folder = "config";

            //foreach (var arc in configs)
            //{
            //    string hash = arc;
            //    string save = $@"{hash.Substring(0, 2)}\{hash.Substring(2, 2)}\{hash}";

            //    Directory.CreateDirectory($"f:\\wowdev\\{folder}\\{hash.Substring(0, 2)}\\{hash.Substring(2, 2)}");

            //    Console.WriteLine(hash);

            //    try
            //    {
            //        client.DownloadFile($"http://level3.blizzard.com/tpr/wowdev/{folder}/{hash.Substring(0, 2)}/{hash.Substring(2, 2)}/{hash}", $@"f:\wowdev\{folder}\{save}");
            //    }
            //    catch (Exception exc)
            //    {
            //        Console.WriteLine(exc.Message);
            //    }

            //    try
            //    {
            //        client.DownloadFile($"http://level3.blizzard.com/tpr/wowdev/{folder}/{hash.Substring(0, 2)}/{hash.Substring(2, 2)}/{hash}.index", $@"f:\wowdev\{folder}\{save}.index");
            //    }
            //    catch (Exception exc)
            //    {
            //        Console.WriteLine(exc.Message);
            //    }
            //}

            //CASCConfig.ValidateData = false;

            //foreach (var file in files)
            //{
            //    FileInfo fi = new FileInfo(file);

            //    if (fi.Exists && fi.Length > 100 * 1024 * 1024)
            //    {
            //        string outFile = Path.Combine(@"f:\\wowdev\\decrypted", Path.GetFileName(file));
            //        string outFile2 = Path.Combine(@"f:\\wowdev\\unpacked", Path.GetFileName(file));

            //        //using (FileStream output = new FileStream(outFile, FileMode.Create))
            //        //    using (var input = fi.OpenRead())
            //        //using (var x = crypt.DecryptFileToStream(Path.GetFileNameWithoutExtension(file), input))
            //        //    x.CopyTo(output);

            //        using (FileStream fsi = new FileStream(outFile, FileMode.Open))
            //        using (FileStream fso = new FileStream(outFile2, FileMode.Create))
            //        using (BLTEStream blte = new BLTEStream(fsi, default))
            //        {
            //            blte.CopyTo(fso);
            //        }
            //    }
            //}

            // http://level3.blizzard.com/tpr/wowdev/patch/04/9e/049eb9b929a5aac89ddce183a0489383
            // http://level3.blizzard.com/tpr/wowdev/patch/04/9e/049eb9b929a5aac89ddce183a0489383.index

            //WebClient client = new WebClient();
            //client.DownloadFile("http://level3.blizzard.com/tpr/wowdev/data/65/f3/65f3aaac573aac170b734b8972b6689d.index", "65f3aaac573aac170b734b8972b6689d.index");

            //Tests t = new Tests();
            //t.Test();

            //using (FileStream fs = new FileStream("65f3aaac573aac170b734b8972b6689d.index.2", FileMode.Open))
            //{
            //    FileIndexHandler fileIndex = new FileIndexHandler(fs);

            //    var data = fileIndex.Data;

            //    foreach (var item in data)
            //    {
            //        string hash = item.ToHexString().ToLower();
            //        string save = $@"{hash.Substring(0, 2)}\{hash.Substring(2, 2)}\{hash}";

            //        Directory.CreateDirectory($"f:\\wowdev\\data\\{hash.Substring(0, 2)}\\{hash.Substring(2, 2)}");

            //        if (File.Exists($"f:\\wowdev\\{hash}"))
            //        {
            //            File.Move($"f:\\wowdev\\{hash}", $"f:\\wowdev\\data\\{save}");
            //        }
            //        else
            //        {
            //            Console.WriteLine($"File {hash} doesnt exist!");
            //        }

            //        //try
            //        //{
            //        //    client.DownloadFile($"http://level3.blizzard.com/tpr/wowdev/data/{hash.Substring(0, 2)}/{hash.Substring(2, 2)}/{hash}", $@"f:\wowdev\{hash}");
            //        //}
            //        //catch (Exception exc)
            //        //{
            //        //    Console.WriteLine(exc.Message);
            //        //}

            //        //try
            //        //{
            //        //    client.DownloadFile($"http://level3.blizzard.com/tpr/wowdev/data/{hash.Substring(0, 2)}/{hash.Substring(2, 2)}/{hash}.index", $@"f:\wowdev\{hash}.index");
            //        //}
            //        //catch (Exception exc)
            //        //{
            //        //    Console.WriteLine(exc.Message);
            //        //}
            //    }
            //}

            var commandsBinder = new CASCConsoleOptionsBinder();

            commandsBinder.Root.SetHandler((CASCConsoleOptions options) => {
                Extract(options.Mode, options.ModeParam, options.Region, options.DestFolder, options.Locale, options.Product, options.Online, options.StoragePath, options.OverrideArchive, options.PreferHighResTextures);
            }, commandsBinder);
            commandsBinder.Root.Invoke(args);
        }

        private static void Extract(ExtractMode mode, string modeParam, string region, string destFolder, LocaleFlags locale, string product, bool online, string storagePath, bool overrideArchive, bool preferHighResTextures)
        {
            DateTime startTime = DateTime.Now;

            Console.WriteLine($"Started at {startTime}");

            Console.WriteLine("Extract params:");
            Console.WriteLine("  Mode: {0}", mode);
            Console.WriteLine("  Mode Param: {0}", modeParam);
            Console.WriteLine("  Region: {0}", region);
            Console.WriteLine("  Destination: {0}", destFolder);
            Console.WriteLine("  LocaleFlags: {0}", locale);
            Console.WriteLine("  Product: {0}", product);
            Console.WriteLine("  Online: {0}", online);
            Console.WriteLine("  Storage Path: {0}", storagePath);
            Console.WriteLine("  OverrideArchive: {0}", overrideArchive);
            Console.WriteLine("  PreferHighResTextures: {0}", preferHighResTextures);

            Console.WriteLine("Loading...");

            BackgroundWorkerEx bgLoader = new BackgroundWorkerEx();
            bgLoader.ProgressChanged += BgLoader_ProgressChanged;

            CASCConfig.LoadFlags |= LoadFlags.Install;

            CASCConfig config = online
                ? CASCConfig.LoadOnlineStorageConfig(product, region)
                : CASCConfig.LoadLocalStorageConfig(storagePath, product);

            CASCHandler cascHandler = CASCHandler.OpenStorage(config, bgLoader);

            cascHandler.Root.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.csv"), bgLoader);
            CASCFolder root = cascHandler.Root.SetFlags(locale, overrideArchive, preferHighResTextures);
            cascHandler.Root.MergeInstall(cascHandler.Install);

            Console.WriteLine($"Loaded {config.Product} {config.VersionName}");

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
