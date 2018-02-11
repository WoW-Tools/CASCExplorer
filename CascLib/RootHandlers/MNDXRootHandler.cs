using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CASCLib
{
    public struct MNDXHeader
    {
        public int Signature;                            // 'MNDX'
        public int HeaderVersion;                        // Must be <= 2
        public int FormatVersion;
    }

    public struct MARInfo
    {
        public int MarIndex;
        public int MarDataSize;
        public int MarDataSizeHi;
        public int MarDataOffset;
        public int MarDataOffsetHi;
    }

    public struct TRIPLET
    {
        public int BaseValue;
        public int Value2;
        public int Value3;
    }

    public struct NAME_FRAG
    {
        public int ItemIndex;   // Back index to various tables
        public int NextIndex;   // The following item index
        public int FragOffs;    // Higher 24 bits are 0xFFFFFF00 --> A single matching character
                                // Otherwise --> Offset to the name fragment table
    }

    class CASC_ROOT_ENTRY_MNDX
    {
        public MD5Hash MD5;         // Encoding key for the file
        public int Flags;           // High 8 bits: Flags, low 24 bits: package index
        public int FileSize;        // Uncompressed file size, in bytes
        public CASC_ROOT_ENTRY_MNDX Next;
    }

    public class PATH_STOP
    {
        public int ItemIndex { get; set; }
        public int Field_4 { get; set; }
        public int Field_8 { get; set; }
        public int Field_C { get; set; }
        public int Field_10 { get; set; }

        public PATH_STOP()
        {
            ItemIndex = 0;
            Field_4 = 0;
            Field_8 = 0;
            Field_C = -1;
            Field_10 = -1;
        }
    }

    class MNDXRootHandler : RootHandlerBase
    {
        private const int CASC_MNDX_SIGNATURE = 0x58444E4D;          // 'MNDX'
        private const int CASC_MAX_MAR_FILES = 3;

        //[0] - package names
        //[1] - file names stripped off package names
        //[2] - complete file names
        private MARFileNameDB[] MarFiles = new MARFileNameDB[CASC_MAX_MAR_FILES];

        private Dictionary<int, CASC_ROOT_ENTRY_MNDX> mndxRootEntries = new Dictionary<int, CASC_ROOT_ENTRY_MNDX>();
        private Dictionary<int, CASC_ROOT_ENTRY_MNDX> mndxRootEntriesValid;

        private Dictionary<int, string> Packages = new Dictionary<int, string>();
        private Dictionary<int, LocaleFlags> PackagesLocale = new Dictionary<int, LocaleFlags>();

        private Dictionary<ulong, RootEntry> mndxData = new Dictionary<ulong, RootEntry>();

        public override int Count { get { return MarFiles[2].NumFiles; } }
        public override int CountTotal { get { return MarFiles[2].NumFiles; } }

        public MNDXRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            var header = stream.Read<MNDXHeader>();

            if (header.Signature != CASC_MNDX_SIGNATURE || header.FormatVersion > 2 || header.FormatVersion < 1)
                throw new Exception("invalid root file");

            if (header.HeaderVersion == 2)
            {
                var build1 = stream.ReadInt32(); // build number
                var build2 = stream.ReadInt32(); // build number
            }

            int MarInfoOffset = stream.ReadInt32();                            // Offset of the first MAR entry info
            int MarInfoCount = stream.ReadInt32();                             // Number of the MAR info entries
            int MarInfoSize = stream.ReadInt32();                              // Size of the MAR info entry
            int MndxEntriesOffset = stream.ReadInt32();
            int MndxEntriesTotal = stream.ReadInt32();                         // Total number of MNDX root entries
            int MndxEntriesValid = stream.ReadInt32();                         // Number of valid MNDX root entries
            int MndxEntrySize = stream.ReadInt32();                            // Size of one MNDX root entry

            if (MarInfoCount > CASC_MAX_MAR_FILES || MarInfoSize != Marshal.SizeOf<MARInfo>())
                throw new Exception("invalid root file (1)");

            for (int i = 0; i < MarInfoCount; i++)
            {
                stream.BaseStream.Position = MarInfoOffset + (MarInfoSize * i);

                MARInfo marInfo = stream.Read<MARInfo>();

                stream.BaseStream.Position = marInfo.MarDataOffset;

                MarFiles[i] = new MARFileNameDB(stream);

                if (stream.BaseStream.Position != marInfo.MarDataOffset + marInfo.MarDataSize)
                    throw new Exception("MAR parsing error!");
            }

            //if (MndxEntrySize != Marshal.SizeOf(typeof(CASC_ROOT_ENTRY_MNDX)))
            //    throw new Exception("invalid root file (2)");

            stream.BaseStream.Position = MndxEntriesOffset;

            CASC_ROOT_ENTRY_MNDX prevEntry = null;

            //Dictionary<int, int> p = new Dictionary<int, int>();

            for (int i = 0; i < MndxEntriesTotal; i++)
            {
                CASC_ROOT_ENTRY_MNDX entry = new CASC_ROOT_ENTRY_MNDX();

                if (prevEntry != null)
                    prevEntry.Next = entry;

                prevEntry = entry;
                entry.Flags = stream.ReadInt32();
                entry.MD5 = stream.Read<MD5Hash>();
                entry.FileSize = stream.ReadInt32();
                mndxRootEntries.Add(i, entry);

                //if ((entry.Flags & 0x80000000) != 0)
                //{
                //    if (!p.ContainsKey(entry.Flags & 0x00FFFFFF))
                //        p[entry.Flags & 0x00FFFFFF] = 1;
                //    else
                //        p[entry.Flags & 0x00FFFFFF]++;
                //}

                worker?.ReportProgress((int)((i + 1) / (float)MndxEntriesTotal * 100));
            }

            //for (int i = 0; i < MndxEntriesTotal; ++i)
            //    Logger.WriteLine("{0:X8} - {1:X8} - {2}", i, mndxRootEntries[i].Flags, mndxRootEntries[i].MD5.ToHexString());

            mndxRootEntriesValid = new Dictionary<int, CASC_ROOT_ENTRY_MNDX>();// mndxRootEntries.Where(e => (e.Flags & 0x80000000) != 0).ToList();

            //var e1 = mndxRootEntries.Where(e => (e.Value.Flags & 0x80000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e2 = mndxRootEntries.Where(e => (e.Value.Flags & 0x40000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e3 = mndxRootEntries.Where(e => (e.Value.Flags & 0x20000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e4 = mndxRootEntries.Where(e => (e.Value.Flags & 0x10000000) != 0).ToDictionary(e => e.Key, e => e.Value);

            //var e5 = mndxRootEntries.Where(e => (e.Value.Flags & 0x8000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e6 = mndxRootEntries.Where(e => (e.Value.Flags & 0x4000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e7 = mndxRootEntries.Where(e => (e.Value.Flags & 0x2000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e8 = mndxRootEntries.Where(e => (e.Value.Flags & 0x1000000) != 0).ToDictionary(e => e.Key, e => e.Value);

            //var e9 = mndxRootEntries.Where(e => (e.Value.Flags & 0x4000000) == 0).ToDictionary(e => e.Key, e => e.Value);

            //int c = 0;
            //foreach(var e in e9)
            //    Console.WriteLine("{0:X8} - {1:X8} - {2:X8} - {3}", c++,e.Key, e.Value.Flags, e.Value.EncodingKey.ToHexString());

            int ValidEntryCount = 1; // edx
            int index = 0;

            mndxRootEntriesValid[index++] = mndxRootEntries[0];

            for (int i = 0; i < MndxEntriesTotal; i++)
            {
                if (ValidEntryCount >= MndxEntriesValid)
                    break;

                if ((mndxRootEntries[i].Flags & 0x80000000) != 0)
                {
                    mndxRootEntriesValid[index++] = mndxRootEntries[i + 1];

                    ValidEntryCount++;
                }
            }

            //for (int i = 0, j = 0; i < MndxEntriesTotal; i++, j++)
            //{
            //    if ((mndxRootEntries[i].Flags & 0x80000000) != 0)
            //    {
            //        mndxRootEntriesValid[j] = mndxRootEntries[i];
            //    }
            //}
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            return mndxData;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (mndxData.TryGetValue(hash, out RootEntry rootEntry))
                yield return rootEntry;
        }

        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            //RootEntry rootEntry;
            //mndxData.TryGetValue(hash, out rootEntry);

            //if (rootEntry != null)
            //    yield return rootEntry;
            //else
            //    yield break;
            //return GetAllEntries(hash);
            return GetEntriesForSelectedLocale(hash);
        }

        private int FindMNDXPackage(string fileName)
        {
            int nMaxLength = 0;
            int pMatching = -1;

            int fileNameLen = fileName.Length;

            foreach (var package in Packages)
            {
                string pkgName = package.Value;
                int pkgNameLen = pkgName.Length;

                if (pkgNameLen < fileNameLen && pkgNameLen > nMaxLength)
                {
                    // Compare the package name
                    if (string.CompareOrdinal(fileName, 0, pkgName, 0, pkgNameLen) == 0)
                    {
                        pMatching = package.Key;
                        nMaxLength = pkgNameLen;
                    }
                }
            }

            return pMatching;
        }

        private CASC_ROOT_ENTRY_MNDX FindMNDXInfo(string path, int dwPackage)
        {
            MNDXSearchResult result = new MNDXSearchResult()
            {
                SearchMask = path.Substring(Packages[dwPackage].Length + 1).ToLower()
            };
            MARFileNameDB marFile1 = MarFiles[1];

            if (marFile1.FindFileInDatabase(result))
            {
                var pRootEntry = mndxRootEntriesValid[result.FileNameIndex];

                while ((pRootEntry.Flags & 0x00FFFFFF) != dwPackage)
                {
                    // The highest bit serves as a terminator if set
                    if ((pRootEntry.Flags & 0x80000000) != 0)
                        throw new Exception("File not found!");

                    pRootEntry = pRootEntry.Next;
                }

                // Give the root entry pointer to the caller
                return pRootEntry;
            }

            throw new Exception("File not found!");
        }

        private CASC_ROOT_ENTRY_MNDX FindMNDXInfo2(string path, int dwPackage)
        {
            MNDXSearchResult result = new MNDXSearchResult()
            {
                SearchMask = path
            };
            MARFileNameDB marFile2 = MarFiles[2];

            if (marFile2.FindFileInDatabase(result))
            {
                var pRootEntry = mndxRootEntries[result.FileNameIndex];

                while ((pRootEntry.Flags & 0x00FFFFFF) != dwPackage)
                {
                    // The highest bit serves as a terminator if set
                    //if ((pRootEntry.Flags & 0x80000000) != 0)
                    //    throw new Exception("File not found!");

                    pRootEntry = pRootEntry.Next;
                }

                // Give the root entry pointer to the caller
                return pRootEntry;
            }

            throw new Exception("File not found!");
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            worker?.ReportProgress(0, "Loading \"listfile\"...");

            Logger.WriteLine("MNDXRootHandler: loading file names...");

            //MNDXSearchResult result = new MNDXSearchResult();

            //MARFileNameDB marFile0 = MarFiles[0];

            Regex regex1 = new Regex("\\w{4}(?=\\.(storm|sc2)data)", RegexOptions.Compiled);
            Regex regex2 = new Regex("\\w{4}(?=\\.(storm|sc2)assets)", RegexOptions.Compiled);

            foreach (var result in MarFiles[0].EnumerateFiles())
            {
                Packages.Add(result.FileNameIndex, result.FoundPath);

                Match match1 = regex1.Match(result.FoundPath);
                Match match2 = regex2.Match(result.FoundPath);

                if (match1.Success || match2.Success)
                {
                    var localeStr = match1.Success ? match1.Value : match2.Value;

                    if (!Enum.TryParse(localeStr, true, out LocaleFlags locale))
                        locale = LocaleFlags.All;

                    PackagesLocale.Add(result.FileNameIndex, locale);
                }
                else
                    PackagesLocale.Add(result.FileNameIndex, LocaleFlags.All);
            }

            //MNDXSearchResult result2 = new MNDXSearchResult();

            //MARFileNameDB marFile2 = MarFiles[2];

            //result.SetSearchPath("mods/heroes.stormmod/base.stormassets/Assets/Sounds/Ambient_3D/Amb_3D_Birds_FlyAway01.ogg");
            //bool res = MarFiles[0].FindFileInDatabase(result);
            //result.SetSearchPath("mods/heroes.stormmod/base.stormassets/Assets/Textures/tyrael_spec.dds");
            //bool res = MarFiles[1].FindFileInDatabase(result);

            //int pkg = FindMNDXPackage("mods/heroes.stormmod/eses.stormassets/localizeddata/sounds/vo/tyrael_ping_defendthing00.ogg");

            //var info1 = FindMNDXInfo("mods/heroes.stormmod/eses.stormassets/localizeddata/sounds/vo/tyrael_ping_defendthing00.ogg", pkg);

            //var info2 = FindMNDXInfo2("mods/heroes.stormmod/eses.stormassets/localizeddata/sounds/vo/tyrael_ping_defendthing00.ogg", pkg);

            //var info3 = FindMNDXInfo2("mods/heroes.stormmod/eses.stormassets/LocalizedData/Sounds/VO/Tyrael_Ping_DefendThing00.ogg", pkg);

            int i = 0;

            foreach (var result in MarFiles[2].EnumerateFiles())
            {
                string file = result.FoundPath;

                ulong fileHash = Hasher.ComputeHash(file);

                CASCFile.Files[fileHash] = new CASCFile(fileHash, file);

                RootEntry entry = new RootEntry();

                int package = FindMNDXPackage(file);
                entry.LocaleFlags = PackagesLocale[package];
                entry.ContentFlags = ContentFlags.None;
                entry.MD5 = FindMNDXInfo(file, package).MD5;
                mndxData[fileHash] = entry;

                //Console.WriteLine("{0:X8} - {1:X8} - {2} - {3}", result2.FileNameIndex, root.Flags, root.EncodingKey.ToHexString(), file);

                worker?.ReportProgress((int)(++i / (float)MarFiles[2].NumFiles * 100));
            }

            //var sorted = data.OrderBy(e => e.Key);
            //foreach (var e in sorted)
            //    Console.WriteLine("{0:X8} - {1:X8} - {2}", e.Key, e.Value.Flags, e.Value.EncodingKey.ToHexString());

            Logger.WriteLine("MNDXRootHandler: loaded {0} file names", i);
        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;

            foreach (var entry in mndxData)
            {
                if ((entry.Value.LocaleFlags & Locale) == 0)
                    continue;

                CreateSubTree(root, entry.Key, CASCFile.Files[entry.Key].FullName);
                CountSelect++;
            }

            return root;
        }

        public override void Clear()
        {
            mndxData.Clear();
            mndxRootEntries.Clear();
            mndxRootEntriesValid.Clear();
            Packages.Clear();
            PackagesLocale.Clear();
            Root.Entries.Clear();
            CASCFile.Files.Clear();
        }

        public override void Dump()
        {

        }
    }

    class MARFileNameDB
    {
        private const int CASC_MAR_SIGNATURE = 0x0052414d;           // 'MAR\0'

        private TSparseArray Struct68_00;
        private TSparseArray FileNameIndexes;
        private TSparseArray Struct68_D0;
        private byte[] FrgmDist_LoBits;
        private TBitEntryArray FrgmDist_HiBits;
        private TNameIndexStruct IndexStruct_174;
        private MARFileNameDB NextDB;
        private NAME_FRAG[] NameFragTable;
        private int NameFragIndexMask;
        private int field_214;

        public int NumFiles { get { return FileNameIndexes.ValidItemCount; } }

        private byte[] table_1BA1818 =
        {
            0x07, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x07, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x07, 0x07, 0x07, 0x01, 0x07, 0x02, 0x02, 0x01, 0x07, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x06, 0x06, 0x01, 0x06, 0x02, 0x02, 0x01, 0x06, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x07, 0x07, 0x01, 0x07, 0x02, 0x02, 0x01, 0x07, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x06, 0x06, 0x01, 0x06, 0x02, 0x02, 0x01, 0x06, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x02, 0x07, 0x07, 0x07, 0x03, 0x07, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x02, 0x07, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x02, 0x07, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x02, 0x07, 0x06, 0x06, 0x03, 0x06, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x02, 0x06, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x02, 0x06, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x02, 0x07, 0x07, 0x07, 0x03, 0x07, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x02, 0x07, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x02, 0x07, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x02, 0x07, 0x06, 0x06, 0x03, 0x06, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x02, 0x06, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x02, 0x06, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04, 0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x03,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04, 0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04, 0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x03,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04, 0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04, 0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x03,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04, 0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04, 0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x03,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04, 0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07
        };

        public MARFileNameDB(BinaryReader reader, bool next = false)
        {
            if (!next && reader.ReadInt32() != CASC_MAR_SIGNATURE)
                throw new Exception("invalid MAR file");

            Struct68_00 = new TSparseArray(reader);
            FileNameIndexes = new TSparseArray(reader);
            Struct68_D0 = new TSparseArray(reader);
            FrgmDist_LoBits = reader.ReadArray<byte>();
            FrgmDist_HiBits = new TBitEntryArray(reader);
            IndexStruct_174 = new TNameIndexStruct(reader);

            if (Struct68_D0.ValidItemCount != 0 && IndexStruct_174.Count == 0)
            {
                NextDB = new MARFileNameDB(reader, true);
            }

            NameFragTable = reader.ReadArray<NAME_FRAG>();

            NameFragIndexMask = NameFragTable.Length - 1;

            field_214 = reader.ReadInt32();

            int dwBitMask = reader.ReadInt32();
        }

        private int sub_1959CB0(int dwItemIndex)
        {
            TRIPLET pTriplet;
            int dwKeyShifted = (dwItemIndex >> 9);
            int eax, ebx, ecx, edx, esi, edi;

            // If lower 9 is zero
            edx = dwItemIndex;
            if ((edx & 0x1FF) == 0)
                return Struct68_00.GetArrayValue_38(dwKeyShifted);

            eax = Struct68_00.GetArrayValue_38(dwKeyShifted) >> 9;
            esi = (Struct68_00.GetArrayValue_38(dwKeyShifted + 1) + 0x1FF) >> 9;
            dwItemIndex = esi;

            if ((eax + 0x0A) >= esi)
            {
                // HOTS: 1959CF7
                int i = eax + 1;
                pTriplet = Struct68_00.GetBaseValue(i);
                i++;
                edi = (eax << 0x09);
                ebx = edi - pTriplet.BaseValue + 0x200;
                while (edx >= ebx)
                {
                    // HOTS: 1959D14
                    edi += 0x200;
                    pTriplet = Struct68_00.GetBaseValue(i);

                    ebx = edi - pTriplet.BaseValue + 0x200;
                    eax++;
                    i++;
                }
            }
            else
            {
                // HOTS: 1959D2E
                while ((eax + 1) < esi)
                {
                    // HOTS: 1959D38
                    // ecx = Struct68_00.BaseValues.TripletArray;
                    esi = (esi + eax) >> 1;
                    ebx = (esi << 0x09) - Struct68_00.GetBaseValue(esi).BaseValue;
                    if (edx < ebx)
                    {
                        // HOTS: 01959D4B
                        dwItemIndex = esi;
                    }
                    else
                    {
                        // HOTS: 1959D50
                        eax = esi;
                        esi = dwItemIndex;
                    }
                }
            }

            // HOTS: 1959D5F
            pTriplet = Struct68_00.GetBaseValue(eax);
            edx += pTriplet.BaseValue - (eax << 0x09);
            edi = (eax << 4);

            eax = pTriplet.Value2;
            ecx = (eax >> 0x17);
            ebx = 0x100 - ecx;
            if (edx < ebx)
            {
                // HOTS: 1959D8C
                ecx = ((eax >> 0x07) & 0xFF);
                esi = 0x80 - ecx;
                if (edx < esi)
                {
                    // HOTS: 01959DA2
                    eax = eax & 0x7F;
                    ecx = 0x40 - eax;
                    if (edx >= ecx)
                    {
                        // HOTS: 01959DB7
                        edi += 2;
                        edx = edx + eax - 0x40;
                    }
                }
                else
                {
                    // HOTS: 1959DC0
                    eax = (eax >> 0x0F) & 0xFF;
                    esi = 0xC0 - eax;
                    if (edx < esi)
                    {
                        // HOTS: 1959DD3
                        edi += 4;
                        edx = edx + ecx - 0x80;
                    }
                    else
                    {
                        // HOTS: 1959DD3
                        edi += 6;
                        edx = edx + eax - 0xC0;
                    }
                }
            }
            else
            {
                // HOTS: 1959DE8
                esi = pTriplet.Value3;
                eax = ((esi >> 0x09) & 0x1FF);
                ebx = 0x180 - eax;
                if (edx < ebx)
                {
                    // HOTS: 01959E00
                    esi = esi & 0x1FF;
                    eax = (0x140 - esi);
                    if (edx < eax)
                    {
                        // HOTS: 1959E11
                        edi = edi + 8;
                        edx = edx + ecx - 0x100;
                    }
                    else
                    {
                        // HOTS: 1959E1D
                        edi = edi + 0x0A;
                        edx = edx + esi - 0x140;
                    }
                }
                else
                {
                    // HOTS: 1959E29
                    esi = (esi >> 0x12) & 0x1FF;
                    ecx = (0x1C0 - esi);
                    if (edx < ecx)
                    {
                        // HOTS: 1959E3D
                        edi = edi + 0x0C;
                        edx = edx + eax - 0x180;
                    }
                    else
                    {
                        // HOTS: 1959E49
                        edi = edi + 0x0E;
                        edx = edx + esi - 0x1C0;
                    }
                }
            }

            // HOTS: 1959E53:
            // Calculate the number of bits set in the value of "ecx"
            ecx = ~Struct68_00.GetItemBit(edi);
            eax = GetNumberOfSetBits(ecx);
            esi = eax >> 0x18;

            if (edx >= esi)
            {
                // HOTS: 1959ea4
                ecx = ~Struct68_00.GetItemBit(++edi);
                edx = edx - esi;
                eax = GetNumberOfSetBits(ecx);
            }

            // HOTS: 1959eea 
            // ESI gets the number of set bits in the lower 16 bits of ECX
            esi = (eax >> 0x08) & 0xFF;
            edi = edi << 0x05;
            if (edx < esi)
            {
                // HOTS: 1959EFC
                eax = eax & 0xFF;
                if (edx >= eax)
                {
                    // HOTS: 1959F05
                    ecx >>= 0x08;
                    edi += 0x08;
                    edx -= eax;
                }
            }
            else
            {
                // HOTS: 1959F0D
                eax = (eax >> 0x10) & 0xFF;
                if (edx < eax)
                {
                    // HOTS: 1959F19
                    ecx >>= 0x10;
                    edi += 0x10;
                    edx -= esi;
                }
                else
                {
                    // HOTS: 1959F23
                    ecx >>= 0x18;
                    edi += 0x18;
                    edx -= eax;
                }
            }

            // HOTS: 1959f2b
            edx = edx << 0x08;
            ecx = ecx & 0xFF;

            // BUGBUG: Possible buffer overflow here. Happens when dwItemIndex >= 0x9C.
            // The same happens in Heroes of the Storm (build 29049), so I am not sure
            // if this is a bug or a case that never happens
            Debug.Assert((ecx + edx) < table_1BA1818.Length);
            return table_1BA1818[ecx + edx] + edi;
        }

        private int sub_1959F50(int arg_0)
        {
            TRIPLET pTriplet;
            int eax, ebx, ecx, edx, esi, edi;

            edx = arg_0;
            eax = arg_0 >> 0x09;
            if ((arg_0 & 0x1FF) == 0)
                return Struct68_00.GetArrayValue_50(eax);

            int item0 = Struct68_00.GetArrayValue_50(eax);
            int item1 = Struct68_00.GetArrayValue_50(eax + 1);
            eax = (item0 >> 0x09);
            edi = (item1 + 0x1FF) >> 0x09;

            if ((eax + 0x0A) > edi)
            {
                // HOTS: 01959F94
                int i = eax + 1;
                pTriplet = Struct68_00.GetBaseValue(i);
                i++;
                while (edx >= pTriplet.BaseValue)
                {
                    // HOTS: 1959FA3
                    pTriplet = Struct68_00.GetBaseValue(i);
                    eax++;
                    i++;
                }
            }
            else
            {
                // Binary search
                // HOTS: 1959FAD
                if (eax + 1 < edi)
                {
                    // HOTS: 1959FB4
                    esi = (edi + eax) >> 1;
                    if (edx < Struct68_00.GetBaseValue(esi).BaseValue)
                    {
                        // HOTS: 1959FC4
                        edi = esi;
                    }
                    else
                    {
                        // HOTS: 1959FC8
                        eax = esi;
                    }
                }
            }

            // HOTS: 1959FD4
            pTriplet = Struct68_00.GetBaseValue(eax);
            edx = edx - pTriplet.BaseValue;
            edi = eax << 0x04;
            eax = pTriplet.Value2;
            ebx = (eax >> 0x17);
            if (edx < ebx)
            {
                // HOTS: 1959FF1
                esi = (eax >> 0x07) & 0xFF;
                if (edx < esi)
                {
                    // HOTS: 0195A000
                    eax = eax & 0x7F;
                    if (edx >= eax)
                    {
                        // HOTS: 195A007
                        edi = edi + 2;
                        edx = edx - eax;
                    }
                }
                else
                {
                    // HOTS: 195A00E
                    eax = (eax >> 0x0F) & 0xFF;
                    if (edx < eax)
                    {
                        // HOTS: 195A01A
                        edi += 4;
                        edx = edx - esi;
                    }
                    else
                    {
                        // HOTS: 195A01F
                        edi += 6;
                        edx = edx - eax;
                    }
                }
            }
            else
            {
                // HOTS: 195A026
                esi = pTriplet.Value3;
                eax = (pTriplet.Value3 >> 0x09) & 0x1FF;
                if (edx < eax)
                {
                    // HOTS: 195A037
                    esi = esi & 0x1FF;
                    if (edx < esi)
                    {
                        // HOTS: 195A041
                        edi = edi + 8;
                        edx = edx - ebx;
                    }
                    else
                    {
                        // HOTS: 195A048
                        edi = edi + 0x0A;
                        edx = edx - esi;
                    }
                }
                else
                {
                    // HOTS: 195A04D
                    esi = (esi >> 0x12) & 0x1FF;
                    if (edx < esi)
                    {
                        // HOTS: 195A05A
                        edi = edi + 0x0C;
                        edx = edx - eax;
                    }
                    else
                    {
                        // HOTS: 195A061
                        edi = edi + 0x0E;
                        edx = edx - esi;
                    }
                }
            }

            // HOTS: 195A066
            esi = Struct68_00.GetItemBit(edi);
            eax = GetNumberOfSetBits(esi);
            ecx = eax >> 0x18;

            if (edx >= ecx)
            {
                // HOTS: 195A0B2
                esi = Struct68_00.GetItemBit(++edi);
                edx = edx - ecx;
                eax = GetNumberOfSetBits(esi);
            }

            // HOTS: 195A0F6
            ecx = (eax >> 0x08) & 0xFF;

            edi = (edi << 0x05);
            if (edx < ecx)
            {
                // HOTS: 195A111
                eax = eax & 0xFF;
                if (edx >= eax)
                {
                    // HOTS: 195A111
                    edi = edi + 0x08;
                    esi = esi >> 0x08;
                    edx = edx - eax;
                }
            }
            else
            {
                // HOTS: 195A119
                eax = (eax >> 0x10) & 0xFF;
                if (edx < eax)
                {
                    // HOTS: 195A125
                    esi = esi >> 0x10;
                    edi = edi + 0x10;
                    edx = edx - ecx;
                }
                else
                {
                    // HOTS: 195A12F
                    esi = esi >> 0x18;
                    edi = edi + 0x18;
                    edx = edx - eax;
                }
            }

            esi = esi & 0xFF;
            edx = edx << 0x08;

            // BUGBUG: Potential buffer overflow
            // Happens in Heroes of the Storm when arg_0 == 0x5B
            Debug.Assert((esi + edx) < table_1BA1818.Length);
            return table_1BA1818[esi + edx] + edi;
        }

        bool CheckNextPathFragment(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            int CollisionIndex;
            int NameFragIndex;
            int SaveCharIndex;
            int HiBitsIndex;
            int FragOffs;

            // Calculate index of the next name fragment in the name fragment table
            NameFragIndex = ((pStruct40.ItemIndex << 0x05) ^ pStruct40.ItemIndex ^ pStruct1C.SearchMask[pStruct40.CharIndex]) & NameFragIndexMask;

            // Does the hash value match?
            if (NameFragTable[NameFragIndex].ItemIndex == pStruct40.ItemIndex)
            {
                // Check if there is single character match
                if (IsSingleCharMatch(NameFragTable, NameFragIndex))
                {
                    pStruct40.ItemIndex = NameFragTable[NameFragIndex].NextIndex;
                    pStruct40.CharIndex++;
                    return true;
                }

                // Check if there is a name fragment match
                if (NextDB != null)
                {
                    if (!NextDB.sub_1957B80(pStruct1C, NameFragTable[NameFragIndex].FragOffs))
                        return false;
                }
                else
                {
                    if (!IndexStruct_174.CheckNameFragment(pStruct1C, NameFragTable[NameFragIndex].FragOffs))
                        return false;
                }

                pStruct40.ItemIndex = NameFragTable[NameFragIndex].NextIndex;
                return true;
            }

            //
            // Conflict: Multiple hashes give the same table index
            //

            // HOTS: 1957A0E
            CollisionIndex = sub_1959CB0(pStruct40.ItemIndex) + 1;
            if (!Struct68_00.Contains(CollisionIndex))
                return false;

            pStruct40.ItemIndex = (CollisionIndex - pStruct40.ItemIndex - 1);
            HiBitsIndex = -1;

            // HOTS: 1957A41:
            do
            {
                // HOTS: 1957A41
                // Check if the low 8 bits if the fragment offset contain a single character
                // or an offset to a name fragment 
                if (Struct68_D0.Contains(pStruct40.ItemIndex))
                {
                    if (HiBitsIndex == -1)
                    {
                        // HOTS: 1957A6C
                        HiBitsIndex = Struct68_D0.GetItemValue(pStruct40.ItemIndex);
                    }
                    else
                    {
                        // HOTS: 1957A7F
                        HiBitsIndex++;
                    }

                    // HOTS: 1957A83
                    SaveCharIndex = pStruct40.CharIndex;

                    // Get the name fragment offset as combined value from lower 8 bits and upper bits
                    FragOffs = GetNameFragmentOffsetEx(pStruct40.ItemIndex, HiBitsIndex);

                    // Compare the string with the fragment name database
                    if (NextDB != null)
                    {
                        // HOTS: 1957AEC
                        if (NextDB.sub_1957B80(pStruct1C, FragOffs))
                            return true;
                    }
                    else
                    {
                        // HOTS: 1957AF7
                        if (IndexStruct_174.CheckNameFragment(pStruct1C, FragOffs))
                            return true;
                    }

                    // HOTS: 1957B0E
                    // If there was partial match with the fragment, end the search
                    if (pStruct40.CharIndex != SaveCharIndex)
                        return false;
                }
                else
                {
                    // HOTS: 1957B1C
                    if (FrgmDist_LoBits[pStruct40.ItemIndex] == pStruct1C.SearchMask[pStruct40.CharIndex])
                    {
                        pStruct40.CharIndex++;
                        return true;
                    }
                }

                // HOTS: 1957B32
                pStruct40.ItemIndex++;
                CollisionIndex++;
            }
            while (Struct68_00.Contains(CollisionIndex));
            return false;
        }

        private bool sub_1957B80(MNDXSearchResult pStruct1C, int dwKey)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            NAME_FRAG pNameEntry;
            int FragOffs;
            int eax, edi;

            edi = dwKey;

            // HOTS: 1957B95
            for (;;)
            {
                pNameEntry = NameFragTable[(edi & NameFragIndexMask)];
                if (edi == pNameEntry.NextIndex)
                {
                    // HOTS: 01957BB4
                    if ((pNameEntry.FragOffs & 0xFFFFFF00) != 0xFFFFFF00)
                    {
                        // HOTS: 1957BC7
                        if (NextDB != null)
                        {
                            // HOTS: 1957BD3
                            if (!NextDB.sub_1957B80(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                        else
                        {
                            // HOTS: 1957BE0
                            if (!IndexStruct_174.CheckNameFragment(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 1957BEE
                        if (pStruct1C.SearchMask[pStruct40.CharIndex] != (byte)pNameEntry.FragOffs)
                            return false;
                        pStruct40.CharIndex++;
                    }

                    // HOTS: 1957C05
                    edi = pNameEntry.ItemIndex;
                    if (edi == 0)
                        return true;

                    if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                        return false;
                }
                else
                {
                    // HOTS: 1957C30
                    if (Struct68_D0.Contains(edi))
                    {
                        // HOTS: 1957C4C
                        if (NextDB != null)
                        {
                            // HOTS: 1957C58
                            FragOffs = GetNameFragmentOffset(edi);
                            if (!NextDB.sub_1957B80(pStruct1C, FragOffs))
                                return false;
                        }
                        else
                        {
                            // HOTS: 1957350
                            FragOffs = GetNameFragmentOffset(edi);
                            if (!IndexStruct_174.CheckNameFragment(pStruct1C, FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 1957C8E
                        if (FrgmDist_LoBits[edi] != pStruct1C.SearchMask[pStruct40.CharIndex])
                            return false;

                        pStruct40.CharIndex++;
                    }

                    // HOTS: 1957CB2
                    if (edi <= field_214)
                        return true;

                    if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                        return false;

                    eax = sub_1959F50(edi);
                    edi = (eax - edi - 1);
                }
            }
        }

        private void sub_1958D70(MNDXSearchResult pStruct1C, int arg_4)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            NAME_FRAG pNameEntry;

            // HOTS: 1958D84
            for (;;)
            {
                pNameEntry = NameFragTable[(arg_4 & NameFragIndexMask)];
                if (arg_4 == pNameEntry.NextIndex)
                {
                    // HOTS: 1958DA6
                    if ((pNameEntry.FragOffs & 0xFFFFFF00) != 0xFFFFFF00)
                    {
                        // HOTS: 1958DBA
                        if (NextDB != null)
                        {
                            NextDB.sub_1958D70(pStruct1C, pNameEntry.FragOffs);
                        }
                        else
                        {
                            IndexStruct_174.CopyNameFragment(pStruct1C, pNameEntry.FragOffs);
                        }
                    }
                    else
                    {
                        // HOTS: 1958DE7
                        // Insert the low 8 bits to the path being built
                        pStruct40.Add((byte)(pNameEntry.FragOffs & 0xFF));
                    }

                    // HOTS: 1958E71
                    arg_4 = pNameEntry.ItemIndex;
                    if (arg_4 == 0)
                        return;
                }
                else
                {
                    // HOTS: 1958E8E
                    if (Struct68_D0.Contains(arg_4))
                    {
                        int FragOffs;

                        // HOTS: 1958EAF
                        FragOffs = GetNameFragmentOffset(arg_4);
                        if (NextDB != null)
                        {
                            NextDB.sub_1958D70(pStruct1C, FragOffs);
                        }
                        else
                        {
                            IndexStruct_174.CopyNameFragment(pStruct1C, FragOffs);
                        }
                    }
                    else
                    {
                        // HOTS: 1958F50
                        // Insert one character to the path being built
                        pStruct40.Add(FrgmDist_LoBits[arg_4]);
                    }

                    // HOTS: 1958FDE
                    if (arg_4 <= field_214)
                        return;

                    arg_4 = -1 - arg_4 + sub_1959F50(arg_4);
                }
            }
        }

        private bool sub_1959010(MNDXSearchResult pStruct1C, int arg_4)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            NAME_FRAG pNameEntry;

            // HOTS: 1959024
            for (;;)
            {
                pNameEntry = NameFragTable[(arg_4 & NameFragIndexMask)];
                if (arg_4 == pNameEntry.NextIndex)
                {
                    // HOTS: 1959047
                    if ((pNameEntry.FragOffs & 0xFFFFFF00) != 0xFFFFFF00)
                    {
                        // HOTS: 195905A
                        if (NextDB != null)
                        {
                            if (!NextDB.sub_1959010(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                        else
                        {
                            if (!IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 1959092
                        if ((byte)(pNameEntry.FragOffs & 0xFF) != pStruct1C.SearchMask[pStruct40.CharIndex])
                            return false;

                        // Insert the low 8 bits to the path being built
                        pStruct40.Add((byte)(pNameEntry.FragOffs & 0xFF));
                        pStruct40.CharIndex++;
                    }

                    // HOTS: 195912E
                    arg_4 = pNameEntry.ItemIndex;
                    if (arg_4 == 0)
                        return true;
                }
                else
                {
                    // HOTS: 1959147
                    if (Struct68_D0.Contains(arg_4))
                    {
                        int FragOffs;

                        // HOTS: 195917C
                        FragOffs = GetNameFragmentOffset(arg_4);
                        if (NextDB != null)
                        {
                            if (!NextDB.sub_1959010(pStruct1C, FragOffs))
                                return false;
                        }
                        else
                        {
                            if (!IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 195920E
                        if (FrgmDist_LoBits[arg_4] != pStruct1C.SearchMask[pStruct40.CharIndex])
                            return false;

                        // Insert one character to the path being built
                        pStruct40.Add(FrgmDist_LoBits[arg_4]);
                        pStruct40.CharIndex++;
                    }

                    // HOTS: 19592B6
                    if (arg_4 <= field_214)
                        return true;

                    arg_4 = -1 - arg_4 + sub_1959F50(arg_4);
                }

                // HOTS: 19592D5
                if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                    break;
            }

            sub_1958D70(pStruct1C, arg_4);
            return true;
        }

        private bool EnumerateFiles(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (pStruct40.SearchPhase == CASCSearchPhase.Finished)
                return false;

            if (pStruct40.SearchPhase != CASCSearchPhase.Searching)
            {
                // HOTS: 1959489
                pStruct40.InitSearchBuffers();

                // If the caller passed a part of the search path, we need to find that one
                while (pStruct40.CharIndex < pStruct1C.SearchMask.Length)
                {
                    if (!sub_1958B00(pStruct1C))
                    {
                        pStruct40.Finish();
                        return false;
                    }
                }

                // HOTS: 19594b0
                PATH_STOP PathStop = new PATH_STOP()
                {
                    ItemIndex = pStruct40.ItemIndex,
                    Field_4 = 0,
                    Field_8 = pStruct40.NumBytesFound,
                    Field_C = -1,
                    Field_10 = -1
                };
                pStruct40.AddPathStop(PathStop);
                pStruct40.ItemCount = 1;

                if (FileNameIndexes.Contains(pStruct40.ItemIndex))
                {
                    pStruct1C.SetFindResult(pStruct40.Result, FileNameIndexes.GetItemValue(pStruct40.ItemIndex));
                    return true;
                }
            }

            // HOTS: 1959522
            for (;;)
            {
                // HOTS: 1959530
                if (pStruct40.ItemCount == pStruct40.NumPathStops)
                {
                    PATH_STOP pLastStop;
                    int CollisionIndex;

                    pLastStop = pStruct40.GetPathStop(pStruct40.NumPathStops - 1);
                    CollisionIndex = sub_1959CB0(pLastStop.ItemIndex) + 1;

                    // Insert a new structure
                    PATH_STOP PathStop = new PATH_STOP()
                    {
                        ItemIndex = CollisionIndex - pLastStop.ItemIndex - 1,
                        Field_4 = CollisionIndex,
                        Field_8 = 0,
                        Field_C = -1,
                        Field_10 = -1
                    };
                    pStruct40.AddPathStop(PathStop);
                }

                // HOTS: 19595BD
                PATH_STOP pPathStop = pStruct40.GetPathStop(pStruct40.ItemCount);

                // HOTS: 19595CC
                if (Struct68_00.Contains(pPathStop.Field_4++))
                {
                    // HOTS: 19595F2
                    pStruct40.ItemCount++;

                    if (Struct68_D0.Contains(pPathStop.ItemIndex))
                    {
                        // HOTS: 1959617
                        if (pPathStop.Field_C == -1)
                            pPathStop.Field_C = Struct68_D0.GetItemValue(pPathStop.ItemIndex);
                        else
                            pPathStop.Field_C++;

                        // HOTS: 1959630
                        int FragOffs = GetNameFragmentOffsetEx(pPathStop.ItemIndex, pPathStop.Field_C);
                        if (NextDB != null)
                        {
                            // HOTS: 1959649
                            NextDB.sub_1958D70(pStruct1C, FragOffs);
                        }
                        else
                        {
                            // HOTS: 1959654
                            IndexStruct_174.CopyNameFragment(pStruct1C, FragOffs);
                        }
                    }
                    else
                    {
                        // HOTS: 1959665
                        // Insert one character to the path being built
                        pStruct40.Add(FrgmDist_LoBits[pPathStop.ItemIndex]);
                    }

                    // HOTS: 19596AE
                    pPathStop.Field_8 = pStruct40.NumBytesFound;

                    // HOTS: 19596b6
                    if (FileNameIndexes.Contains(pPathStop.ItemIndex))
                    {
                        // HOTS: 19596D1
                        if (pPathStop.Field_10 == -1)
                        {
                            // HOTS: 19596D9
                            pPathStop.Field_10 = FileNameIndexes.GetItemValue(pPathStop.ItemIndex);
                        }
                        else
                        {
                            pPathStop.Field_10++;
                        }

                        // HOTS: 1959755
                        pStruct1C.SetFindResult(pStruct40.Result, pPathStop.Field_10);
                        return true;
                    }
                }
                else
                {
                    // HOTS: 19596E9
                    if (pStruct40.ItemCount == 1)
                    {
                        pStruct40.Finish();
                        return false;
                    }

                    // HOTS: 19596F5
                    pPathStop = pStruct40.GetPathStop(pStruct40.ItemCount - 1);
                    pPathStop.ItemIndex++;

                    pPathStop = pStruct40.GetPathStop(pStruct40.ItemCount - 2);
                    int edi = pPathStop.Field_8;

                    // HOTS: 1959749
                    pStruct40.RemoveRange(edi);
                    pStruct40.ItemCount--;
                }
            }
        }

        private bool sub_1958B00(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            byte[] pbPathName = Encoding.ASCII.GetBytes(pStruct1C.SearchMask);
            int CollisionIndex;
            int FragmentOffset;
            int SaveCharIndex;
            int ItemIndex;
            int FragOffs;
            int var_4;

            ItemIndex = pbPathName[pStruct40.CharIndex] ^ (pStruct40.ItemIndex << 0x05) ^ pStruct40.ItemIndex;
            ItemIndex = ItemIndex & NameFragIndexMask;
            if (pStruct40.ItemIndex == NameFragTable[ItemIndex].ItemIndex)
            {
                // HOTS: 1958B45
                FragmentOffset = NameFragTable[ItemIndex].FragOffs;
                if ((FragmentOffset & 0xFFFFFF00) == 0xFFFFFF00)
                {
                    // HOTS: 1958B88
                    pStruct40.Add((byte)FragmentOffset);
                    pStruct40.ItemIndex = NameFragTable[ItemIndex].NextIndex;
                    pStruct40.CharIndex++;
                    return true;
                }

                // HOTS: 1958B59
                if (NextDB != null)
                {
                    if (!NextDB.sub_1959010(pStruct1C, FragmentOffset))
                        return false;
                }
                else
                {
                    if (!IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, FragmentOffset))
                        return false;
                }

                // HOTS: 1958BCA
                pStruct40.ItemIndex = NameFragTable[ItemIndex].NextIndex;
                return true;
            }

            // HOTS: 1958BE5
            CollisionIndex = sub_1959CB0(pStruct40.ItemIndex) + 1;
            if (!Struct68_00.Contains(CollisionIndex))
                return false;

            pStruct40.ItemIndex = (CollisionIndex - pStruct40.ItemIndex - 1);
            var_4 = -1;

            // HOTS: 1958C20
            for (;;)
            {
                if (Struct68_D0.Contains(pStruct40.ItemIndex))
                {
                    // HOTS: 1958C0E
                    if (var_4 == -1)
                    {
                        // HOTS: 1958C4B
                        var_4 = Struct68_D0.GetItemValue(pStruct40.ItemIndex);
                    }
                    else
                    {
                        var_4++;
                    }

                    // HOTS: 1958C62
                    SaveCharIndex = pStruct40.CharIndex;

                    FragOffs = GetNameFragmentOffsetEx(pStruct40.ItemIndex, var_4);
                    if (NextDB != null)
                    {
                        // HOTS: 1958CCB
                        if (NextDB.sub_1959010(pStruct1C, FragOffs))
                            return true;
                    }
                    else
                    {
                        // HOTS: 1958CD6
                        if (IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, FragOffs))
                            return true;
                    }

                    // HOTS: 1958CED
                    if (SaveCharIndex != pStruct40.CharIndex)
                        return false;
                }
                else
                {
                    // HOTS: 1958CFB
                    if (FrgmDist_LoBits[pStruct40.ItemIndex] == pStruct1C.SearchMask[pStruct40.CharIndex])
                    {
                        // HOTS: 1958D11
                        pStruct40.Add(FrgmDist_LoBits[pStruct40.ItemIndex]);
                        pStruct40.CharIndex++;
                        return true;
                    }
                }

                // HOTS: 1958D11
                pStruct40.ItemIndex++;
                CollisionIndex++;

                if (!Struct68_00.Contains(CollisionIndex))
                    break;
            }

            return false;
        }

        public bool FindFileInDatabase(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            pStruct40.ItemIndex = 0;
            pStruct40.CharIndex = 0;
            pStruct40.Init();

            if (pStruct1C.SearchMask.Length > 0)
            {
                while (pStruct40.CharIndex < pStruct1C.SearchMask.Length)
                {
                    // HOTS: 01957F12
                    if (!CheckNextPathFragment(pStruct1C))
                        return false;
                }
            }

            // HOTS: 1957F26
            if (!FileNameIndexes.Contains(pStruct40.ItemIndex))
                return false;

            pStruct1C.SetFindResult(pStruct1C.SearchMask, FileNameIndexes.GetItemValue(pStruct40.ItemIndex));
            return true;
        }

        public IEnumerable<MNDXSearchResult> EnumerateFiles()
        {
            MNDXSearchResult pStruct1C = new MNDXSearchResult();

            while (EnumerateFiles(pStruct1C))
                yield return pStruct1C;
        }

        private int GetNameFragmentOffsetEx(int LoBitsIndex, int HiBitsIndex)
        {
            return (FrgmDist_HiBits[HiBitsIndex] << 0x08) | FrgmDist_LoBits[LoBitsIndex];
        }

        private int GetNameFragmentOffset(int LoBitsIndex)
        {
            return GetNameFragmentOffsetEx(LoBitsIndex, Struct68_D0.GetItemValue(LoBitsIndex));
        }

        private bool IsSingleCharMatch(NAME_FRAG[] Table, int ItemIndex)
        {
            return ((Table[ItemIndex].FragOffs & 0xFFFFFF00) == 0xFFFFFF00);
        }

        private int GetNumberOfSetBits(int Value32)
        {
            Value32 = ((Value32 >> 1) & 0x55555555) + (Value32 & 0x55555555);
            Value32 = ((Value32 >> 2) & 0x33333333) + (Value32 & 0x33333333);
            Value32 = ((Value32 >> 4) & 0x0F0F0F0F) + (Value32 & 0x0F0F0F0F);

            return (Value32 * 0x01010101);
        }
    }

    public class TBitEntryArray : List<int>
    {
        private int BitsPerEntry;
        private int EntryBitMask;
        private int TotalEntries;

        public new int this[int index]
        {
            get
            {
                int dwItemIndex = (index * BitsPerEntry) >> 0x05;
                int dwStartBit = (index * BitsPerEntry) & 0x1F;
                int dwEndBit = dwStartBit + BitsPerEntry;
                int dwResult;

                // If the end bit index is greater than 32,
                // we also need to load from the next 32-bit item
                if (dwEndBit > 0x20)
                {
                    dwResult = (base[dwItemIndex + 1] << (0x20 - dwStartBit)) | (int)((uint)base[dwItemIndex] >> dwStartBit);
                }
                else
                {
                    dwResult = base[dwItemIndex] >> dwStartBit;
                }

                // Now we also need to mask the result by the bit mask
                return dwResult & EntryBitMask;
            }
        }

        public TBitEntryArray(BinaryReader reader) : base(reader.ReadArray<int>())
        {
            BitsPerEntry = reader.ReadInt32();
            EntryBitMask = reader.ReadInt32();
            TotalEntries = (int)reader.ReadInt64();
        }
    }

    public class TSparseArray
    {
        private int[] ItemBits;                    // Bit array for each item (1 = item is present)
        private TRIPLET[] BaseValues;              // Array of base values for item indexes >= 0x200
        private int[] ArrayDwords_38;
        private int[] ArrayDwords_50;

        public int TotalItemCount { get; private set; } // Total number of items in the array
        public int ValidItemCount { get; private set; } // Number of present items in the array

        public TSparseArray(BinaryReader reader)
        {
            ItemBits = reader.ReadArray<int>();
            TotalItemCount = reader.ReadInt32();
            ValidItemCount = reader.ReadInt32();
            BaseValues = reader.ReadArray<TRIPLET>();
            ArrayDwords_38 = reader.ReadArray<int>();
            ArrayDwords_50 = reader.ReadArray<int>();
        }

        public bool Contains(int index)
        {
            return (ItemBits[index >> 0x05] & (1 << (index & 0x1F))) != 0;
        }

        public int GetItemBit(int index)
        {
            return ItemBits[index];
        }

        public TRIPLET GetBaseValue(int index)
        {
            return BaseValues[index];
        }

        public int GetArrayValue_38(int index)
        {
            return ArrayDwords_38[index];
        }

        public int GetArrayValue_50(int index)
        {
            return ArrayDwords_50[index];
        }

        public int GetItemValue(int index)
        {
            TRIPLET pTriplet;
            int DwordIndex;
            int BaseValue;
            int BitMask;

            // 
            // Divide the low-8-bits index to four parts:
            //
            // |-----------------------|---|------------|
            // |       A (23 bits)     | B |      C     |
            // |-----------------------|---|------------|
            //
            // A (23-bits): Index to the table (60 bits per entry)
            //
            //    Layout of the table entry:
            //   |--------------------------------|-------|--------|--------|---------|---------|---------|---------|-----|
            //   |  Base Value                    | val[0]| val[1] | val[2] | val[3]  | val[4]  | val[5]  | val[6]  |  -  |
            //   |  32 bits                       | 7 bits| 8 bits | 8 bits | 9 bits  | 9 bits  | 9 bits  | 9 bits  |5bits|
            //   |--------------------------------|-------|--------|--------|---------|---------|---------|---------|-----|
            //
            // B (3 bits) : Index of the variable-bit value in the array (val[#], see above)
            //
            // C (32 bits): Number of bits to be checked (up to 0x3F bits).
            //              Number of set bits is then added to the values obtained from A and B

            // Upper 23 bits contain index to the table
            pTriplet = BaseValues[index >> 0x09];
            BaseValue = pTriplet.BaseValue;

            // Next 3 bits contain the index to the VBR
            switch (((index >> 0x06) & 0x07) - 1)
            {
                case 0:     // Add the 1st value (7 bits)
                    BaseValue += (pTriplet.Value2 & 0x7F);
                    break;

                case 1:     // Add the 2nd value (8 bits)
                    BaseValue += (pTriplet.Value2 >> 0x07) & 0xFF;
                    break;

                case 2:     // Add the 3rd value (8 bits)
                    BaseValue += (pTriplet.Value2 >> 0x0F) & 0xFF;
                    break;

                case 3:     // Add the 4th value (9 bits)
                    BaseValue += (pTriplet.Value2 >> 0x17) & 0x1FF;
                    break;

                case 4:     // Add the 5th value (9 bits)
                    BaseValue += (pTriplet.Value3 & 0x1FF);
                    break;

                case 5:     // Add the 6th value (9 bits)
                    BaseValue += (pTriplet.Value3 >> 0x09) & 0x1FF;
                    break;

                case 6:     // Add the 7th value (9 bits)
                    BaseValue += (pTriplet.Value3 >> 0x12) & 0x1FF;
                    break;
            }

            //
            // Take the upper 27 bits as an index to DWORD array, take lower 5 bits
            // as number of bits to mask. Then calculate number of set bits in the value
            // masked value.
            //

            // Get the index into the array of DWORDs
            DwordIndex = (index >> 0x05);

            // Add number of set bits in the masked value up to 0x3F bits
            if ((index & 0x20) != 0)
                BaseValue += GetNumbrOfSetBits32(ItemBits[DwordIndex - 1]);

            BitMask = (1 << (index & 0x1F)) - 1;
            return BaseValue + GetNumbrOfSetBits32(ItemBits[DwordIndex] & BitMask);
        }

        private int GetNumberOfSetBits(int Value32)
        {
            Value32 = ((Value32 >> 1) & 0x55555555) + (Value32 & 0x55555555);
            Value32 = ((Value32 >> 2) & 0x33333333) + (Value32 & 0x33333333);
            Value32 = ((Value32 >> 4) & 0x0F0F0F0F) + (Value32 & 0x0F0F0F0F);

            return (Value32 * 0x01010101);
        }

        private int GetNumbrOfSetBits32(int x)
        {
            return (GetNumberOfSetBits(x) >> 0x18);
        }
    }

    public class TNameIndexStruct
    {
        private byte[] NameFragments;
        private TSparseArray FragmentEnds;

        public int Count
        {
            get { return NameFragments.Length; }
        }

        public TNameIndexStruct(BinaryReader reader)
        {
            NameFragments = reader.ReadArray<byte>();
            FragmentEnds = new TSparseArray(reader);
        }

        public bool CheckAndCopyNameFragment(MNDXSearchResult pStruct1C, int dwFragOffs)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (FragmentEnds.TotalItemCount == 0)
            {
                string szSearchMask = pStruct1C.SearchMask;

                int startPos = dwFragOffs - pStruct40.CharIndex;

                // Keep copying as long as we don't reach the end of the search mask
                while (pStruct40.CharIndex < pStruct1C.SearchMask.Length)
                {
                    // HOTS: 195A5A0
                    if (NameFragments[startPos + pStruct40.CharIndex] != szSearchMask[pStruct40.CharIndex])
                        return false;

                    // HOTS: 195A5B7
                    pStruct40.Add(NameFragments[startPos + pStruct40.CharIndex]);
                    pStruct40.CharIndex++;

                    if (NameFragments[startPos + pStruct40.CharIndex] == 0)
                        return true;
                }

                // HOTS: 195A660
                // Now we need to copy the rest of the fragment
                while (NameFragments[startPos + pStruct40.CharIndex] != 0)
                {
                    pStruct40.Add(NameFragments[startPos + pStruct40.CharIndex]);
                    startPos++;
                }
            }
            else
            {
                // Get the offset of the fragment to compare
                // HOTS: 195A6B7
                string szSearchMask = pStruct1C.SearchMask;

                // Keep copying as long as we don't reach the end of the search mask
                while (dwFragOffs < pStruct1C.SearchMask.Length)
                {
                    if (NameFragments[dwFragOffs] != szSearchMask[pStruct40.CharIndex])
                        return false;

                    pStruct40.Add(NameFragments[dwFragOffs]);
                    pStruct40.CharIndex++;

                    // Keep going as long as the given bit is not set
                    if (FragmentEnds.Contains(dwFragOffs++))
                        return true;
                }

                // Now we need to copy the rest of the fragment
                while (!FragmentEnds.Contains(dwFragOffs))
                {
                    // HOTS: 195A7A6
                    pStruct40.Add(NameFragments[dwFragOffs]);
                    dwFragOffs++;
                }
            }

            return true;
        }

        public bool CheckNameFragment(MNDXSearchResult pStruct1C, int dwFragOffs)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (FragmentEnds.TotalItemCount == 0)
            {
                // Get the offset of the fragment to compare. For convenience with pStruct40->CharIndex,
                // subtract the CharIndex from the fragment offset
                string szSearchMask = pStruct1C.SearchMask;

                int startPos = dwFragOffs - pStruct40.CharIndex;

                // Keep searching as long as the name matches with the fragment
                while (NameFragments[startPos + pStruct40.CharIndex] == szSearchMask[pStruct40.CharIndex])
                {
                    // Move to the next character
                    pStruct40.CharIndex++;

                    // Is it the end of the fragment or end of the path?
                    if (NameFragments[startPos + pStruct40.CharIndex] == 0)
                        return true;

                    if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                        return false;
                }

                return false;
            }
            else
            {
                // Get the offset of the fragment to compare.
                string szSearchMask = pStruct1C.SearchMask;

                // Keep searching as long as the name matches with the fragment
                while (NameFragments[dwFragOffs] == szSearchMask[pStruct40.CharIndex])
                {
                    // Move to the next character
                    pStruct40.CharIndex++;

                    // Is it the end of the fragment or end of the path?
                    if (FragmentEnds.Contains(dwFragOffs++))
                        return true;

                    if (dwFragOffs >= pStruct1C.SearchMask.Length)
                        return false;
                }

                return false;
            }
        }

        public void CopyNameFragment(MNDXSearchResult pStruct1C, int dwFragOffs)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (FragmentEnds.TotalItemCount == 0)
            {
                while (NameFragments[dwFragOffs] != 0)
                {
                    pStruct40.Add(NameFragments[dwFragOffs++]);
                }
            }
            else
            {
                while (!FragmentEnds.Contains(dwFragOffs))
                {
                    pStruct40.Add(NameFragments[dwFragOffs++]);
                }
            }
        }
    }

    public enum CASCSearchPhase
    {
        Initializing = 0,
        Searching = 2,
        Finished = 4
    }

    public class SearchBuffer
    {
        private List<byte> SearchResult = new List<byte>();
        private List<PATH_STOP> PathStops = new List<PATH_STOP>();   // Array of path checkpoints

        public int ItemIndex { get; set; } = 0;// Current name fragment: Index to various tables
        public int CharIndex { get; set; } = 0;
        public int ItemCount { get; set; } = 0;
        public CASCSearchPhase SearchPhase { get; private set; } = CASCSearchPhase.Initializing; // 0 = initializing, 2 = searching, 4 = finished

        public string Result
        {
            get { return Encoding.ASCII.GetString(SearchResult.ToArray()); }
        }

        public int NumBytesFound
        {
            get { return SearchResult.Count; }
        }

        public int NumPathStops
        {
            get { return PathStops.Count; }
        }

        public void Add(byte value)
        {
            SearchResult.Add(value);
        }

        public void RemoveRange(int index)
        {
            SearchResult.RemoveRange(index, SearchResult.Count - index);
        }

        public void AddPathStop(PATH_STOP item)
        {
            PathStops.Add(item);
        }

        public PATH_STOP GetPathStop(int index)
        {
            return PathStops[index];
        }

        public void Init()
        {
            SearchPhase = CASCSearchPhase.Initializing;
        }

        public void Finish()
        {
            SearchPhase = CASCSearchPhase.Finished;
        }

        public void InitSearchBuffers()
        {
            SearchResult.Clear();
            PathStops.Clear();

            ItemIndex = 0;
            CharIndex = 0;
            ItemCount = 0;
            SearchPhase = CASCSearchPhase.Searching;
        }
    }

    public class MNDXSearchResult
    {
        private string szSearchMask;
        public string SearchMask        // Search mask without wildcards
        {
            get { return szSearchMask; }
            set
            {
                Buffer.Init();

                szSearchMask = value ?? throw new ArgumentNullException(nameof(SearchMask));
            }
        }
        public string FoundPath { get; private set; }       // Found path name
        public int FileNameIndex { get; private set; }      // Index of the file name
        public SearchBuffer Buffer { get; private set; }

        public MNDXSearchResult()
        {
            Buffer = new SearchBuffer();

            SearchMask = string.Empty;
        }

        public void SetFindResult(string szFoundPath, int dwFileNameIndex)
        {
            FoundPath = szFoundPath;
            FileNameIndex = dwFileNameIndex;
        }
    }
}
