using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCLib
{
    public interface ICASCEntry
    {
        string Name { get; }
        ulong Hash { get; }
        int CompareTo(ICASCEntry entry, int col, CASCHandler casc);
    }

    public class CASCFolder : ICASCEntry
    {
        public Dictionary<string, ICASCEntry> Entries { get; set; }

        public CASCFolder(string name)
        {
            Entries = new Dictionary<string, ICASCEntry>(StringComparer.OrdinalIgnoreCase);
            Name = name;
        }

        public string Name { get; private set; }

        public ulong Hash => 0;

        public ICASCEntry GetEntry(string name)
        {
            Entries.TryGetValue(name, out ICASCEntry entry);
            return entry;
        }

        public static IEnumerable<CASCFile> GetFiles(IEnumerable<ICASCEntry> entries, IEnumerable<int> selection = null, bool recursive = true)
        {
            var entries2 = selection != null ? selection.Select(index => entries.ElementAt(index)) : entries;

            foreach (var entry in entries2)
            {
                if (entry is CASCFile file1)
                {
                    yield return file1;
                }
                else
                {
                    if (recursive)
                    {
                        var folder = entry as CASCFolder;

                        foreach (var file in GetFiles(folder.Entries.Select(kv => kv.Value)))
                        {
                            yield return file;
                        }
                    }
                }
            }
        }

        public int CompareTo(ICASCEntry other, int col, CASCHandler casc)
        {
            int result = 0;

            if (other is CASCFile)
                return -1;

            switch (col)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    result = Name.CompareTo(other.Name);
                    break;
                case 4:
                    break;
            }

            return result;
        }
    }

    public class CASCFile : ICASCEntry
    {
        public CASCFile(ulong hash, string fullname)
        {
            Hash = hash;
            FullName = fullname;
        }

        public string Name => Path.GetFileName(FullName);

        public string FullName { get; set; }

        public ulong Hash { get; private set; }

        public long GetSize(CASCHandler casc) => casc.GetEncodingEntry(Hash, out EncodingEntry enc) ? enc.Size : 0;

        public int CompareTo(ICASCEntry other, int col, CASCHandler casc)
        {
            int result = 0;

            if (other is CASCFolder)
                return 1;

            switch (col)
            {
                case 0:
                    result = Name.CompareTo(other.Name);
                    break;
                case 1:
                    result = Path.GetExtension(Name).CompareTo(Path.GetExtension(other.Name));
                    break;
                case 2:
                    {
                        var e1 = casc.Root.GetEntries(Hash);
                        var e2 = casc.Root.GetEntries(other.Hash);
                        var flags1 = e1.Any() ? e1.First().LocaleFlags : LocaleFlags.None;
                        var flags2 = e2.Any() ? e2.First().LocaleFlags : LocaleFlags.None;
                        result = flags1.CompareTo(flags2);
                    }
                    break;
                case 3:
                    {
                        var e1 = casc.Root.GetEntries(Hash);
                        var e2 = casc.Root.GetEntries(other.Hash);
                        var flags1 = e1.Any() ? e1.First().ContentFlags : ContentFlags.None;
                        var flags2 = e2.Any() ? e2.First().ContentFlags : ContentFlags.None;
                        result = flags1.CompareTo(flags2);
                    }
                    break;
                case 4:
                    var size1 = GetSize(casc);
                    var size2 = (other as CASCFile).GetSize(casc);

                    if (size1 == size2)
                        result = 0;
                    else
                        result = size1 < size2 ? -1 : 1;
                    break;
            }

            return result;
        }

        public static readonly Dictionary<ulong, CASCFile> Files = new Dictionary<ulong, CASCFile>();
    }
}
