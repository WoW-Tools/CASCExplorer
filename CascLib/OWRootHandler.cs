using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CASCExplorer
{
    public class OWRootHandler : RootHandlerBase
    {
        private readonly Dictionary<ulong, RootEntry> RootData = new Dictionary<ulong, RootEntry>();

        public unsafe OWRootHandler(BinaryReader stream, BackgroundWorkerEx worker, CASCHandler casc)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            string str = Encoding.ASCII.GetString(stream.ReadBytes((int)stream.BaseStream.Length));

            string[] array = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // need to figure out what to do with those apm files

            for (int i = 1; i < array.Length; i++)
            {
                string[] filedata = array[i].Split('|');

                string name = filedata[4];

                if (Path.GetExtension(name) == ".apm")
                {
                    // add apm file for dev purposes
                    ulong fileHash1 = Hasher.ComputeHash(name);
                    MD5Hash md5 = filedata[0].ToByteArray().ToMD5();
                    RootData[fileHash1] = new RootEntry() { MD5 = md5, LocaleFlags = LocaleFlags.All, ContentFlags = ContentFlags.None };

                    CASCFile.FileNames[fileHash1] = name;

                    // add files listed in apm file
                    EncodingEntry enc;

                    if (!casc.Encoding.GetEntry(md5, out enc))
                        continue;

                    using (Stream s = casc.OpenFile(enc.Key))
                    using (BinaryReader br = new BinaryReader(s))
                    {
                        // still need to figure out complete apm structure
                        // at start of file there's a lot of data that is same in all apm files
                        s.Position = 0xC;

                        uint count = br.ReadUInt32();

                        s.Position = 0x930;

                        // size of each entry seems to be 0x48 bytes (0x2C bytes unk data; int size; ulong unk; byte[16] md5)
                        for (int j = 0; j < count; j++)
                        {
                            s.Position += 0x2C; // skip unknown
                            int size = br.ReadInt32(); // size (matches size in encoding file)
                            s.Position += 8; // skip unknown
                            MD5Hash md5_2 = br.Read<MD5Hash>();

                            EncodingEntry enc2;

                            if (!casc.Encoding.GetEntry(md5_2, out enc2))
                            {
                                throw new Exception("enc2 == null");
                            }

                            string fakeName = Path.GetFileNameWithoutExtension(name) + "/" + md5_2.ToHexString();

                            ulong fileHash = Hasher.ComputeHash(fakeName);
                            RootData[fileHash] = new RootEntry() { MD5 = md5_2, LocaleFlags = LocaleFlags.All, ContentFlags = ContentFlags.None };

                            CASCFile.FileNames[fileHash] = fakeName;
                        }
                    }
                }
            }

            int current = 0;

            Func<string, LocaleFlags> tag2locale = (s) =>
            {
                LocaleFlags locale;

                if (Enum.TryParse(s, out locale))
                    return locale;

                return LocaleFlags.All;
            };

            MD5Hash key;

            foreach (var entry in casc.Encoding.Entries)
            {
                key = entry.Key;

                string fakeName = "unknown" + "/" + key.Value[0].ToString("X2") + "/" + entry.Key.ToHexString();

                ulong fileHash = Hasher.ComputeHash(fakeName);
                RootData.Add(fileHash, new RootEntry() { MD5 = entry.Key, LocaleFlags = LocaleFlags.All, ContentFlags = ContentFlags.None });

                CASCFile.FileNames[fileHash] = fakeName;

                worker?.ReportProgress((int)(++current / (float)casc.Encoding.Count * 100));
            }
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            foreach (var entry in RootData)
                yield return entry;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            RootEntry entry;

            if (RootData.TryGetValue(hash, out entry))
                yield return entry;
            else
                yield break;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            //RootEntry entry;

            //if (RootData.TryGetValue(hash, out entry))
            //    yield return entry;
            //else
            //    yield break;
            return GetAllEntries(hash);
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {

        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;
            CountUnknown = 0;

            foreach (var rootEntry in RootData)
            {
                if ((rootEntry.Value.LocaleFlags & Locale) == 0)
                    continue;

                CreateSubTree(root, rootEntry.Key, CASCFile.FileNames[rootEntry.Key]);
                CountSelect++;
            }

            Logger.WriteLine("OWRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            RootData.Clear();
            Root.Entries.Clear();
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {

        }
    }
}
