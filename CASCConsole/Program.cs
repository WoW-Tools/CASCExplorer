using CASCConsole.Properties;
using CASCLib;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CASCConsole
{
    class Program
    {
        static readonly object ProgressLock = new object();

        //class Hashes
        //{
        //    public string[] install;
        //    public string[] encoding;
        //}

        static void Main(string[] args)
        {
            //HashSet<string> data = new HashSet<string>();
            //HashSet<string> installs = new HashSet<string>();

            //using (StreamReader sr = new StreamReader("list.txt"))
            //{
            //    string line1;

            //    while((line1 = sr.ReadLine()) != null)
            //    {
            //        data.Add(line1.Substring(19, 32));

            //        if (line1.Contains("install"))
            //        {
            //            installs.Add(line1.Substring(93, 32));
            //        }
            //    }
            //}

            ////foreach (var cfg in data)
            ////{
            ////    string url = string.Format("https://bnet.marlam.in/tpr/wow/config/{0}/{1}/{2}", cfg.Substring(0, 2), cfg.Substring(2, 2), cfg);

            ////    var stream = CDNIndexHandler.OpenFileDirect(url);

            ////    using (var fileStream = File.Create("builds\\" + cfg))
            ////    {
            ////        stream.CopyTo(fileStream);
            ////    }
            ////}

            //Dictionary<string, Hashes> data2 = new Dictionary<string, Hashes>();

            //foreach (var file in Directory.GetFiles("builds"))
            //{
            //    using(var sr = new StreamReader(file))
            //    {
            //        string line;

            //        while ((line = sr.ReadLine()) != null)
            //        {
            //            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) // skip empty lines and comments
            //                continue;

            //            string[] tokens = line.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);

            //            if (tokens.Length != 2)
            //                throw new Exception("KeyValueConfig: tokens.Length != 2");

            //            var values = tokens[1].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            //            var valuesList = values.ToList();

            //            if (!data2.ContainsKey(file))
            //                data2[file] = new Hashes();

            //            if (values[0] == "install")
            //            {
            //                data2[file].install = values;
            //            }

            //            if (values[0] == "encoding")
            //            {
            //                data2[file].encoding = values;
            //            }

            //            //sr.Data.Add(tokens[0].Trim(), valuesList);
            //        }
            //    }
            //}

            //Dictionary<string, string> realInstalls = new Dictionary<string, string>();

            //foreach(var kv in data2)
            //{
            //    for(int i = 6; i < kv.Value.install.Length; i++)
            //    {
            //        if (installs.Contains(kv.Value.install[i]))
            //        {
            //            //Console.WriteLine("{0} {1}", kv.Value.install[i], kv.Value.encoding[i]);

            //            realInstalls[kv.Value.install[i]] = kv.Value.encoding[i];
            //        }
            //    }
            //}

            //CASCConfig.ValidateData = false;

            //int buildIndex = 0;

            //foreach (var kvinstall in realInstalls)
            //{
            //    string url1 = string.Format("http://bnet.marlamin.com/tpr/wow/data/{0}/{1}/{2}", kvinstall.Key.Substring(0, 2), kvinstall.Key.Substring(2, 2), kvinstall.Key);

            //    BLTEStream instFile = new BLTEStream(CDNIndexHandler.OpenFileDirect(url1), new MD5Hash());

            //    InstallHandler install = new InstallHandler(new BinaryReader(instFile), null);

            //    //foreach(var ent  in install.GetEntries().Where(e => e.Name.Contains("MacOS")))
            //    //{
            //    //    Console.WriteLine(ent.Name);
            //    //}
            //    //continue;

            //    string url2 = string.Format("http://bnet.marlamin.com/tpr/wow/data/{0}/{1}/{2}", kvinstall.Value.Substring(0, 2), kvinstall.Value.Substring(2, 2), kvinstall.Value);

            //    BLTEStream encFile = new BLTEStream(CDNIndexHandler.OpenFileDirect(url2), new MD5Hash());

            //    EncodingHandler encoding = new EncodingHandler(new BinaryReader(encFile), null);

            //    string[] files = new string[] { "WowB.exe", "WowT.exe", "Wow.exe", "WowB-64.exe", "WowT-64.exe", "Wow-64.exe", "RenderService.exe", "RenderService-64.exe",
            //        @"World of Warcraft Public Test.app\Contents\MacOS\World of Warcraft",
            //        @"World of Warcraft Public Test.app\Contents\MacOS\World of Warcraft 64",
            //        @"World of Warcraft Beta.app\Contents\MacOS\World of Warcraft",
            //        @"World of Warcraft Beta.app\Contents\MacOS\World of Warcraft 64",
            //        @"World of Warcraft Retail.app\Contents\MacOS\World of Warcraft",
            //        @"World of Warcraft Retail.app\Contents\MacOS\World of Warcraft 64",
            //        @"World of Warcraft Test.app\Contents\MacOS\World of Warcraft",
            //        @"World of Warcraft.app\Contents\MacOS\World of Warcraft"
            //    };

            //    string outFolder = "build_" + buildIndex++;

            //    foreach (var file in files)
            //    {
            //        var entries = install.GetEntriesByName(file);

            //        foreach (var entry in entries)
            //        {
            //            bool ok = encoding.GetEntry(entry.MD5, out var encEntry);

            //            if (ok)
            //            {
            //                string chash = encEntry.Key.ToHexString().ToLower();

            //                Console.WriteLine("http://bnet.marlamin.com/tpr/wow/data/{0}/{1}/{2} {3}", chash.Substring(0, 2), chash.Substring(2, 2), chash, file);

            //                string url = string.Format("http://blzddist1-a.akamaihd.net/tpr/wow/data/{0}/{1}/{2}", chash.Substring(0, 2), chash.Substring(2, 2), chash);
            //                //string url = string.Format("http://bnet.marlamin.com/tpr/wow/data/{0}/{1}/{2}", chash.Substring(0, 2), chash.Substring(2, 2), chash);

            //                try
            //                {
            //                    var stream = CDNIndexHandler.OpenFileDirect(url);

            //                    BLTEStream blte3 = new BLTEStream(stream, new MD5Hash());

            //                    string outFile = Path.Combine(outFolder, chash + "_" + file);
            //                    string outDir = Path.GetDirectoryName(outFile);

            //                    if (!Directory.Exists(outDir))
            //                        Directory.CreateDirectory(outDir);

            //                    using (var fileStream = File.Create(outFile))
            //                    {
            //                        blte3.CopyTo(fileStream);
            //                    }
            //                }
            //                catch(Exception exc)
            //                {
            //                    Console.WriteLine(exc.Message);
            //                }
            //            }
            //        }
            //    }
            //}

            //ArmadilloCrypt crypt = new ArmadilloCrypt("sc1Dev");

            //var decr = crypt.DecryptFile(@"c:\Users\TOM_RUS\Downloads\e32f46c7245bfc154e43924555a5cf9f");

            //File.WriteAllBytes("test", decr);

            //byte[] keyBytes = new byte[16];

            //ArmadilloCrypt crypt = new ArmadilloCrypt(keyBytes);

            //string buildconfigfile = "9f6048f8bd01f38ec0be83f4a9fe5a10";

            //byte[] data = File.ReadAllBytes(buildconfigfile);

            //byte[] IV = buildconfigfile.Substring(16).ToByteArray();

            //unsafe
            //{
            //    fixed (byte* ptr = keyBytes)
            //    {
            //        for (ulong i = 0; i < ulong.MaxValue; i++)
            //        {
            //            for (ulong j = 0; j < ulong.MaxValue; j++)
            //            {
            //                byte[] decrypted = crypt.DecryptFile(IV, data);

            //                if (decrypted[0] == 0x23 && decrypted[1] == 0x20 && decrypted[2] == 0x42 && decrypted[3] == 0x75)
            //                {
            //                    Console.WriteLine("key found: {0} {1} ?", i, j);
            //                }

            //                *(ulong*)ptr = j;

            //                if (j % 1000000 == 0)
            //                    Console.WriteLine("{0}/{1}", j, ulong.MaxValue);
            //            }

            //            *(ulong*)(ptr + 8) = i;
            //        }
            //    }
            //}

            if (args.Length != 5)
            {
                Console.WriteLine("Invalid arguments count!");
                Console.WriteLine("Usage: CASCConsole <mode> <pattern|listfile> <destination> <localeFlags> <contentFlags>");
                return;
            }

            Console.WriteLine("Settings:");
            Console.WriteLine("    WowPath: {0}", Settings.Default.StoragePath);
            Console.WriteLine("    OnlineMode: {0}", Settings.Default.OnlineMode);

            Console.WriteLine("Loading...");

            BackgroundWorkerEx bgLoader = new BackgroundWorkerEx();
            bgLoader.ProgressChanged += BgLoader_ProgressChanged;

            //CASCConfig.LoadFlags |= LoadFlags.Install;

            CASCConfig config = Settings.Default.OnlineMode
                ? CASCConfig.LoadOnlineStorageConfig(Settings.Default.Product, "us")
                : CASCConfig.LoadLocalStorageConfig(Settings.Default.StoragePath);

            CASCHandler cascHandler = CASCHandler.OpenStorage(config, bgLoader);

            string mode = args[0];
            string pattern = args[1];
            string dest = args[2];
            LocaleFlags locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), args[3]);
            ContentFlags content = (ContentFlags)Enum.Parse(typeof(ContentFlags), args[4]);

            cascHandler.Root.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.txt"), bgLoader);
            CASCFolder root = cascHandler.Root.SetFlags(locale, content);
            //cascHandler.Root.MergeInstall(cascHandler.Install);

            Console.WriteLine("Loaded.");

            Console.WriteLine("Extract params:");
            Console.WriteLine("    Mode: {0}", mode);
            Console.WriteLine("    Pattern: {0}", pattern);
            Console.WriteLine("    Destination: {0}", dest);
            Console.WriteLine("    LocaleFlags: {0}", locale);
            Console.WriteLine("    ContentFlags: {0}", content);

            if (mode == "pattern")
            {
                Wildcard wildcard = new Wildcard(pattern, true, RegexOptions.IgnoreCase);

                foreach (var file in CASCFolder.GetFiles(root.Entries.Select(kv => kv.Value)))
                {
                    if (wildcard.IsMatch(file.FullName))
                        ExtractFile(cascHandler, file.FullName, dest);
                }
            }
            else if (mode == "listfile")
            {
                var names = File.ReadLines(pattern);

                foreach (var file in names)
                    ExtractFile(cascHandler, file, dest);
            }

            Console.WriteLine("Extracted.");
        }

        private static void ExtractFile(CASCHandler cascHandler, string file, string dest)
        {
            Console.Write("Extracting '{0}'...", file);

            try
            {
                cascHandler.SaveFileTo(file, dest);
                Console.WriteLine(" Ok!");
            }
            catch (Exception exc)
            {
                Console.WriteLine(" Error!");
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
