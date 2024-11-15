using CASCLib;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace CASCConsole
{
    enum ExtractMode
    {
        Pattern,
        Listfile
    }

    class ExtractionOptions
    {
        public ExtractMode Mode { get; set; }
        public string ModeParam { get; set; }
        public string DestFolder { get; set; }
        public LocaleFlags Locale { get; set; }
        public string Product { get; set; }
        public bool Online { get; set; }
        public string StoragePath { get; set; }
        public bool OverrideArchive { get; set; }
        public bool PreferHighResTextures { get; set; }
        public int Threads { get; set; }
    }

    internal class OptionsBinder : BinderBase<ExtractionOptions>
    {
        private readonly Option<ExtractMode> modeOption = new Option<ExtractMode>(new[] { "-m", "--mode" }, "Extraction mode") { IsRequired = true };
        private readonly Option<string> modeParamOption = new Option<string>(new[] { "-e", "--eparam" }, "Extraction mode parameter (example: *.* or listfile.csv)") { IsRequired = true };
        private readonly Option<string> destOption = new Option<string>(new[] { "-d", "--dest" }, "Destination folder path") { IsRequired = true };
        private readonly Option<LocaleFlags> localeOption = new Option<LocaleFlags>(new[] { "-l", "--locale" }, "Product locale") { IsRequired = true };
        private readonly Option<string> productOption = new Option<string>(new[] { "-p", "--product" }, "Product uid") { IsRequired = true };
        private readonly Option<bool> onlineOption = new Option<bool>(new[] { "-o", "--online" }, () => false, "Online extraction mode");
        private readonly Option<string> storagePathOption = new Option<string>(new[] { "-s", "--storage" }, () => "", "Local game storage folder");
        private readonly Option<bool> overrideArchiveOption = new Option<bool>(new[] { "-a", "--archive" }, () => false, "Override archive");
        private readonly Option<bool> preferHighResTexturesOption = new Option<bool>(new[] { "-h", "--highres" }, () => false, "High Resolution Textures");
        private readonly Option<int> threads = new Option<int>(new[] { "-t", "--threads" }, () => 1, "Number of threads to use for extraction (default 1)");

        public RootCommand Root { get; }

        public OptionsBinder()
        {
            Root = new RootCommand("CASCConsole") { modeOption, modeParamOption, destOption, localeOption, productOption, onlineOption, storagePathOption, overrideArchiveOption, preferHighResTexturesOption, threads };
        }

        protected override ExtractionOptions GetBoundValue(BindingContext bindingContext)
        {
            var parseResult = bindingContext.ParseResult;

            return new ExtractionOptions
            {
                Mode = parseResult.GetValueForOption(modeOption),
                ModeParam = parseResult.GetValueForOption(modeParamOption),
                DestFolder = parseResult.GetValueForOption(destOption),
                Locale = parseResult.GetValueForOption(localeOption),
                Product = parseResult.GetValueForOption(productOption),
                Online = parseResult.GetValueForOption(onlineOption),
                StoragePath = parseResult.GetValueForOption(storagePathOption),
                OverrideArchive = parseResult.GetValueForOption(overrideArchiveOption),
                PreferHighResTextures = parseResult.GetValueForOption(preferHighResTexturesOption),
                Threads = parseResult.GetValueForOption(threads),
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

            var commandsBinder = new OptionsBinder();
            commandsBinder.Root.SetHandler((ExtractionOptions options) => Extract(options), commandsBinder);
            commandsBinder.Root.Invoke(args);
        }

        class ParallelExtractionTask : IDisposable
        {
            private readonly Thread thread;
            private readonly ExtractionOptions config;
            private CASCHandler handler;
            private CASCFolder root;
            private readonly int threadIndex;
            private readonly int threadCount;
            private event Action Finished;


            // Creates parallel tasks and starts them, returns master task that can be Start'ed
            // on the main thread manually. Main task will wait dependents once finished.
            public static ParallelExtractionTask RunParallelExtraction(ExtractionOptions config)
            {
                var master = new ParallelExtractionTask(null, config, 0, config.Threads);
                for (int index = 1; index < config.Threads; index++)
                {
                    _ = new ParallelExtractionTask(master, config, index, config.Threads);
                }
                return master;
            }

            public ParallelExtractionTask(ParallelExtractionTask master, ExtractionOptions config, int threadIndex, int threadCount)
            {
                this.config = config;
                this.threadIndex = threadIndex;
                this.threadCount = threadCount;

                if (master != null)
                {
                    handler = master.handler;
                    root = master.root;
                    thread = new Thread(this.Run);
                    master.Finished += () => thread.Join();
                    thread.Start();
                }
                else
                {
                    // Build hanlder only once for the master task, it takes a lot of memory
                    BuildHandler();
                }
            }

            public void Start()
            {
                Run();
                Finished?.Invoke();
            }

            private void BuildHandler()
            {
                var bgLoader = new BackgroundWorkerEx();
                bgLoader.ProgressChanged += BgLoader_ProgressChanged;

                CASCConfig.LoadFlags |= LoadFlags.Install;

                CASCConfig cascConfig = config.Online
                    ? CASCConfig.LoadOnlineStorageConfig(config.Product, "us")
                    : CASCConfig.LoadLocalStorageConfig(config.StoragePath, config.Product);

                handler = CASCHandler.OpenStorage(cascConfig, bgLoader);
                handler.Root.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.csv"), bgLoader);
                root = handler.Root.SetFlags(this.config.Locale, this.config.OverrideArchive, this.config.PreferHighResTextures);
                handler.Root.MergeInstall(handler.Install);

                Console.WriteLine($"Loaded {cascConfig.Product} {cascConfig.VersionName}");
            }

            private IEnumerable<CASCFile> GetFiles()
            {

                if (config.Mode == ExtractMode.Pattern)
                {
                    var entries = root
                        .Folders
                        .Select(kv => kv.Value as ICASCEntry)
                        .Concat(root.Files.Select(kv => kv.Value));

                    var wildcard = new Wildcard(config.ModeParam, true, RegexOptions.IgnoreCase);
                    return CASCFolder.GetFiles(entries).Where(file => wildcard.IsMatch(file.FullName));
                }

                if (handler.Root is WowRootHandler wowRoot)
                {
                    var splitChar = new char[] { ';' };

                    return File
                        .ReadLines(config.ModeParam)
                        .Select(s => s.Split(splitChar, 2))
                        .Select(s => new CASCFile(ulong.Parse(s[0]), s[1]));
                }


                return File
                    .ReadLines(config.ModeParam)
                    .Select((name) => new CASCFile(0, name));

            }

            private void Run()
            {
                int index = -1;
                foreach (var file in GetFiles())
                {
                    index++;
                    if (index % threadCount != threadIndex) { continue; }

                    ExtractFile(handler, file.Hash, file.FullName, config.DestFolder);
                }
            }

            public void Dispose()
            {
                if (Finished == null) { return; }

                foreach (var a in Finished.GetInvocationList())
                {
                    Finished -= a as Action;
                }
            }
        }

        private static void Extract(ExtractionOptions config)
        {
            DateTime startTime = DateTime.Now;
            Console.WriteLine($"Started at {startTime}");

            Console.WriteLine("Extract params:");
            Console.WriteLine("  Mode: {0}", config.Mode);
            Console.WriteLine("  Mode Param: {0}", config.ModeParam);
            Console.WriteLine("  Destination: {0}", config.DestFolder);
            Console.WriteLine("  LocaleFlags: {0}", config.Locale);
            Console.WriteLine("  Product: {0}", config.Product);
            Console.WriteLine("  Online: {0}", config.Online);
            Console.WriteLine("  Storage Path: {0}", config.StoragePath);
            Console.WriteLine("  OverrideArchive: {0}", config.OverrideArchive);
            Console.WriteLine("  PreferHighResTextures: {0}", config.PreferHighResTextures);

            Console.WriteLine("Loading...");

            var task = ParallelExtractionTask.RunParallelExtraction(config);
            task.Start();

            Console.WriteLine("Extracted.");

            DateTime endTime = DateTime.Now;
            Console.WriteLine($"Ended at {endTime} (took {endTime - startTime})");
        }

        private static void ExtractFile(CASCHandler cascHandler, ulong hash, string file, string dest)
        {
            try
            {
                if (hash != 0)
                    cascHandler.SaveFileTo(hash, dest, file);
                else
                    cascHandler.SaveFileTo(file, dest);

                Console.WriteLine($"Extracting '{file}'... Ok!");
            }
            catch (Exception exc)
            {
                Console.WriteLine($"Extracting '{file}'... Error ({exc.Message})!");
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
