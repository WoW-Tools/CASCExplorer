using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class InstallEntry
    {
        public string Name;
        public MD5Hash MD5;
        public int Size;

        public List<InstallTag> Tags;
    }

    public class InstallTag
    {
        public string Name;
        public short Type;
        public BitArray Bits;
    }

    public class InstallHandler
    {
        private List<InstallEntry> InstallData = new List<InstallEntry>();
        private static readonly Jenkins96 Hasher = new Jenkins96();

        public int Count
        {
            get { return InstallData.Count; }
        }

        public InstallHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"install\"...");

            stream.ReadBytes(2); // IN

            byte b1 = stream.ReadByte();
            byte b2 = stream.ReadByte();
            short numTags = stream.ReadInt16BE();
            int numFiles = stream.ReadInt32BE();

            int numMaskBytes = (numFiles + 7) / 8;

            List<InstallTag> Tags = new List<InstallTag>();

            for (int i = 0; i < numTags; i++)
            {
                InstallTag tag = new InstallTag();
                tag.Name = stream.ReadCString();
                tag.Type = stream.ReadInt16BE();

                byte[] bits = stream.ReadBytes(numMaskBytes);

                for (int j = 0; j < numMaskBytes; j++)
                    bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                tag.Bits = new BitArray(bits);

                Tags.Add(tag);
            }

            for (int i = 0; i < numFiles; i++)
            {
                InstallEntry entry = new InstallEntry();
                entry.Name = stream.ReadCString();
                entry.MD5 = stream.Read<MD5Hash>();
                entry.Size = stream.ReadInt32BE();

                InstallData.Add(entry);

                entry.Tags = Tags.FindAll(tag => tag.Bits[i]);

                worker?.ReportProgress((int)((i + 1) / (float)numFiles * 100));
            }
        }

        public InstallEntry GetEntry(string name)
        {
            return InstallData.Where(i => i.Name == name).FirstOrDefault();
        }

        public IEnumerable<InstallEntry> GetEntries(string tag)
        {
            foreach (var entry in InstallData)
                if (entry.Tags.Any(t => t.Name == tag))
                    yield return entry;
        }

        public IEnumerable<InstallEntry> GetEntries(ulong hash)
        {
            foreach (var entry in InstallData)
                if (Hasher.ComputeHash(entry.Name) == hash)
                    yield return entry;
        }

        public IEnumerable<InstallEntry> GetEntries()
        {
            foreach (var entry in InstallData)
                yield return entry;
        }

        public void Print()
        {
            for (int i = 0; i < InstallData.Count; ++i)
            {
                var data = InstallData[i];

                Logger.WriteLine("{0:D4}: {1} {2}", i, data.MD5.ToHexString(), data.Name);

                Logger.WriteLine("    {0}", string.Join(",", data.Tags.Select(t => t.Name)));
            }
        }

        public void Clear()
        {
            InstallData.Clear();
            InstallData = null;
        }
    }
}
