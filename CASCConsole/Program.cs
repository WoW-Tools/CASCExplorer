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

        static void Main(string[] args)
        {
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

                foreach (var file in CASCFolder.GetFiles(root.Entries.Select(kv => kv.Value), filter: pattern))
                {
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
