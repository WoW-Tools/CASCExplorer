using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    [Flags]
    public enum LocaleFlags : uint
    {
        All = 0xFFFFFFFF,
        None = 0,
        //Unk_1 = 0x1,
        enUS = 0x2,
        koKR = 0x4,
        //Unk_8 = 0x8,
        frFR = 0x10,
        deDE = 0x20,
        zhCN = 0x40,
        esES = 0x80,
        zhTW = 0x100,
        enGB = 0x200,
        enCN = 0x400,
        enTW = 0x800,
        esMX = 0x1000,
        ruRU = 0x2000,
        ptBR = 0x4000,
        itIT = 0x8000,
        ptPT = 0x10000,
        enSG = 0x20000000, // custom
        plPL = 0x40000000, // custom
        All_WoW = enUS | koKR | frFR | deDE | zhCN | esES | zhTW | enGB | esMX | ruRU | ptBR | itIT | ptPT
    }

    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        F00000001 = 0x1,
        F00000002 = 0x2,
        F00000004 = 0x4,
        F00000008 = 0x8, // added in 7.2.0.23436
        F00000010 = 0x10, // added in 7.2.0.23436
        LowViolence = 0x80, // many models have this flag
        F10000000 = 0x10000000,
        F20000000 = 0x20000000, // added in 21737
        Bundle = 0x40000000,
        NoCompression = 0x80000000 // sounds have this flag
    }

    public unsafe struct MD5Hash
    {
        public fixed byte Value[16];
    }

    public struct RootEntry
    {
        public MD5Hash MD5;
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
    }

    public class WowRootHandler : RootHandlerBase
    {
        private MultiDictionary<ulong, RootEntry> RootData = new MultiDictionary<ulong, RootEntry>();
        private Dictionary<int, ulong> FileDataStore = new Dictionary<int, ulong>();
        private Dictionary<ulong, int> FileDataStoreReverse = new Dictionary<ulong, int>();
        private HashSet<ulong> UnknownFiles = new HashSet<ulong>();

        public override int Count => RootData.Count;
        public override int CountTotal => RootData.Sum(re => re.Value.Count);
        public override int CountUnknown => UnknownFiles.Count;

        public WowRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            while (stream.BaseStream.Position < stream.BaseStream.Length)
            {
                int count = stream.ReadInt32();

                ContentFlags contentFlags = (ContentFlags)stream.ReadUInt32();
                LocaleFlags localeFlags = (LocaleFlags)stream.ReadUInt32();

                if (localeFlags == LocaleFlags.None)
                    throw new Exception("block.LocaleFlags == LocaleFlags.None");

                if (contentFlags != ContentFlags.None && (contentFlags & (ContentFlags.F00000008 | ContentFlags.F00000010 | ContentFlags.LowViolence | ContentFlags.NoCompression | ContentFlags.F20000000)) == 0)
                    throw new Exception("block.ContentFlags != ContentFlags.None");

                RootEntry[] entries = new RootEntry[count];
                int[] filedataIds = new int[count];

                int fileDataIndex = 0;

                for (var i = 0; i < count; ++i)
                {
                    entries[i].LocaleFlags = localeFlags;
                    entries[i].ContentFlags = contentFlags;

                    filedataIds[i] = fileDataIndex + stream.ReadInt32();
                    fileDataIndex = filedataIds[i] + 1;
                }

                //Console.WriteLine("Block: {0} {1} (size {2})", block.ContentFlags, block.LocaleFlags, count);

                for (var i = 0; i < count; ++i)
                {
                    entries[i].MD5 = stream.Read<MD5Hash>();

                    ulong hash = stream.ReadUInt64();

                    RootData.Add(hash, entries[i]);

                    //Console.WriteLine("File: {0:X8} {1:X16} {2}", entries[i].FileDataId, hash, entries[i].MD5.ToHexString());

                    int fileDataId = filedataIds[i];

                    if (FileDataStore.TryGetValue(fileDataId, out ulong hash2))
                    {
                        if (hash2 == hash)
                        {
                            // duplicate, skipping
                            continue;
                        }
                        else
                        {
                            Logger.WriteLine("ERROR: got miltiple hashes for filedataid {0}", fileDataId);
                            continue;
                        }
                    }

                    FileDataStore.Add(fileDataId, hash);
                    FileDataStoreReverse.Add(hash, fileDataId);
                }

                worker?.ReportProgress((int)(stream.BaseStream.Position / (float)stream.BaseStream.Length * 100));
            }
        }

        public IEnumerable<RootEntry> GetAllEntriesByFileDataId(int fileDataId) => GetAllEntries(GetHashByFileDataId(fileDataId));

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            foreach (var set in RootData)
                foreach (var entry in set.Value)
                    yield return new KeyValuePair<ulong, RootEntry>(set.Key, entry);
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            RootData.TryGetValue(hash, out List<RootEntry> result);

            if (result == null)
                yield break;

            foreach (var entry in result)
                yield return entry;
        }

        public IEnumerable<RootEntry> GetEntriesByFileDataId(int fileDataId) => GetEntries(GetHashByFileDataId(fileDataId));

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            var rootInfos = GetAllEntries(hash);

            if (!rootInfos.Any())
                yield break;

            var rootInfosLocale = rootInfos.Where(re => (re.LocaleFlags & Locale) != 0);

            if (rootInfosLocale.Count() > 1)
            {
                var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.ContentFlags == Content));

                if (rootInfosLocaleAndContent.Any())
                    rootInfosLocale = rootInfosLocaleAndContent;
            }

            foreach (var entry in rootInfosLocale)
                yield return entry;
        }

        public ulong GetHashByFileDataId(int fileDataId)
        {
            FileDataStore.TryGetValue(fileDataId, out ulong hash);
            return hash;
        }

        public int GetFileDataIdByHash(ulong hash)
        {
            FileDataStoreReverse.TryGetValue(hash, out int fid);
            return fid;
        }

        public int GetFileDataIdByName(string name) => GetFileDataIdByHash(Hasher.ComputeHash(name));

        private bool LoadPreHashedListFile(string pathbin, string pathtext, BackgroundWorkerEx worker = null)
        {
            using (var _ = new PerfCounter("WowRootHandler::LoadPreHashedListFile()"))
            {
                worker?.ReportProgress(0, "Loading \"listfile\"...");

                if (!File.Exists(pathbin))
                    return false;

                var timebin = File.GetLastWriteTime(pathbin);
                var timetext = File.GetLastWriteTime(pathtext);

                if (timebin != timetext) // text has been modified, recreate crehashed file
                    return false;

                Logger.WriteLine("WowRootHandler: loading file names...");

                using (var fs = new FileStream(pathbin, FileMode.Open))
                using (var br = new BinaryReader(fs))
                {
                    int numFolders = br.ReadInt32();

                    for (int i = 0; i < numFolders; i++)
                    {
                        string dirName = br.ReadString();

                        Logger.WriteLine(dirName);

                        int numFiles = br.ReadInt32();

                        for (int j = 0; j < numFiles; j++)
                        {
                            ulong fileHash = br.ReadUInt64();
                            string fileName = br.ReadString();

                            string fileNameFull = dirName != String.Empty ? dirName + "\\" + fileName : fileName;

                            // skip invalid names
                            if (!RootData.ContainsKey(fileHash))
                            {
                                Logger.WriteLine("Invalid file name: {0}", fileNameFull);
                                continue;
                            }

                            CASCFile.FileNames[fileHash] = fileNameFull;
                        }

                        worker?.ReportProgress((int)(fs.Position / (float)fs.Length * 100));
                    }

                    Logger.WriteLine("WowRootHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
                }
            }

            return true;
        }

        public void LoadFileDataComplete(CASCHandler casc)
        {
            if (!casc.FileExists("DBFilesClient\\FileDataComplete.db2"))
                return;

            Logger.WriteLine("WowRootHandler: loading file names from FileDataComplete.db2...");

            using (var s = casc.OpenFile("DBFilesClient\\FileDataComplete.db2"))
            {
                DB5Reader fd = new DB5Reader(s);

                foreach (var row in fd)
                {
                    string path = row.Value.GetField<string>(0);
                    string name = row.Value.GetField<string>(1);

                    string fullname = path + name;

                    ulong fileHash = Hasher.ComputeHash(fullname);

                    // skip invalid names
                    if (!RootData.ContainsKey(fileHash))
                    {
                        //Logger.WriteLine("Invalid file name: {0}", fullname);
                        continue;
                    }

                    CASCFile.FileNames[fileHash] = fullname;
                }
            }
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            if (LoadPreHashedListFile("listfile.bin", path, worker))
                return;

            using (var _ = new PerfCounter("WowRootHandler::LoadListFile()"))
            {
                worker?.ReportProgress(0, "Loading \"listfile\"...");

                if (!File.Exists(path))
                {
                    Logger.WriteLine("WowRootHandler: list file missing!");
                    return;
                }

                Logger.WriteLine("WowRootHandler: loading file names...");

                Dictionary<string, Dictionary<ulong, string>> dirData = new Dictionary<string, Dictionary<ulong, string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [""] = new Dictionary<ulong, string>()
                };
                using (var fs = new FileStream("listfile.bin", FileMode.Create))
                using (var bw = new BinaryWriter(fs))
                using (var fs2 = File.Open(path, FileMode.Open))
                using (var sr = new StreamReader(fs2))
                {
                    string file;

                    while ((file = sr.ReadLine()) != null)
                    {
                        ulong fileHash = Hasher.ComputeHash(file);

                        // skip invalid names
                        if (!RootData.ContainsKey(fileHash))
                        {
                            Logger.WriteLine("Invalid file name: {0}", file);
                            continue;
                        }

                        CASCFile.FileNames[fileHash] = file;

                        int dirSepIndex = file.LastIndexOf('\\');

                        if (dirSepIndex >= 0)
                        {
                            string key = file.Substring(0, dirSepIndex);

                            if (!dirData.ContainsKey(key))
                            {
                                dirData[key] = new Dictionary<ulong, string>();
                            }

                            dirData[key][fileHash] = file.Substring(dirSepIndex + 1);
                        }
                        else
                            dirData[""][fileHash] = file;

                        worker?.ReportProgress((int)(sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }

                    bw.Write(dirData.Count); // count of dirs

                    foreach (var dir in dirData)
                    {
                        bw.Write(dir.Key); // dir name

                        Logger.WriteLine(dir.Key);

                        bw.Write(dirData[dir.Key].Count); // count of files in dir

                        foreach (var fh in dirData[dir.Key])
                        {
                            bw.Write(fh.Key); // file name hash
                            bw.Write(fh.Value); // file name (without dir name)
                        }
                    }

                    Logger.WriteLine("WowRootHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
                }

                File.SetLastWriteTime("listfile.bin", File.GetLastWriteTime(path));
            }
        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            // Reset counts
            CountSelect = 0;
            UnknownFiles.Clear();

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & Locale) != 0);

                if (rootInfosLocale.Count() > 1)
                {
                    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.ContentFlags == Content));

                    if (rootInfosLocaleAndContent.Any())
                        rootInfosLocale = rootInfosLocaleAndContent;
                }

                if (!rootInfosLocale.Any())
                    continue;

                if (!CASCFile.FileNames.TryGetValue(rootEntry.Key, out string file))
                {
                    file = "unknown\\" + rootEntry.Key.ToString("X16") + "_" + GetFileDataIdByHash(rootEntry.Key);

                    UnknownFiles.Add(rootEntry.Key);
                }

                CreateSubTree(root, rootEntry.Key, file);
                CountSelect++;
            }

            Logger.WriteLine("WowRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public bool IsUnknownFile(ulong hash) => UnknownFiles.Contains(hash);

        public override void Clear()
        {
            RootData.Clear();
            RootData = null;
            FileDataStore.Clear();
            FileDataStore = null;
            FileDataStoreReverse.Clear();
            FileDataStoreReverse = null;
            UnknownFiles.Clear();
            UnknownFiles = null;
            Root?.Entries.Clear();
            Root = null;
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {
            foreach (var fd in RootData.OrderBy(r => GetFileDataIdByHash(r.Key)))
            {
                if (!CASCFile.FileNames.TryGetValue(fd.Key, out string name))
                    name = fd.Key.ToString("X16");

                Logger.WriteLine("{0:D7} {1:X16} {2} {3}", GetFileDataIdByHash(fd.Key), fd.Key, fd.Value.Aggregate(LocaleFlags.None, (a, b) => a | b.LocaleFlags), name);

                foreach (var entry in fd.Value)
                {
                    Logger.WriteLine("\t{0} - {1} - {2}", entry.MD5.ToHexString(), entry.LocaleFlags, entry.ContentFlags);
                }
            }
        }
    }
}
