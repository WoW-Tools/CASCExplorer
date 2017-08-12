using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class WC3RootHandler : RootHandlerBase
    {
        private Dictionary<ulong, RootEntry> RootData = new Dictionary<ulong, RootEntry>();

        public WC3RootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            using (StreamReader sr = new StreamReader(stream.BaseStream))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    string[] tokens = line.Split('|');

                    string file;

                    LocaleFlags locale = LocaleFlags.All;

                    if (tokens[0].IndexOf('-') == 4)
                    {
                        string[] tokens2 = tokens[0].Split('-');

                        file = tokens2[1];
                        locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), tokens2[0]);
                    }
                    else
                    {
                        file = tokens[0];
                    }

                    ulong fileHash = Hasher.ComputeHash(file);

                    RootData[fileHash] = new RootEntry()
                    {
                        LocaleFlags = locale,
                        ContentFlags = ContentFlags.None,
                        MD5 = tokens[1].ToByteArray().ToMD5()
                    };

                    CASCFile.FileNames[fileHash] = file;
                }
            }

            worker?.ReportProgress(100);
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            return RootData;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (RootData.TryGetValue(hash, out RootEntry rootEntry))
                yield return rootEntry;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            return GetEntriesForSelectedLocale(hash);
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {

        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;

            foreach (var entry in RootData)
            {
                if ((entry.Value.LocaleFlags & Locale) == 0)
                    continue;

                CreateSubTree(root, entry.Key, CASCFile.FileNames[entry.Key]);
                CountSelect++;
            }

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            Logger.WriteLine("S1RootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            Root.Entries.Clear();
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {

        }
    }
}
