using System.Collections.Generic;
using System.IO;

namespace CASCLib
{
    public struct EncodingEntry
    {
        public MD5Hash Key;
        public long Size;
    }

    public class EncodingHandler
    {
        private static readonly MD5HashComparer comparer = new MD5HashComparer();
        private Dictionary<MD5Hash, EncodingEntry> EncodingData = new Dictionary<MD5Hash, EncodingEntry>(comparer);
        //private TestDictionary<EncodingEntry> EncodingData = new TestDictionary<EncodingEntry>();

        private const int CHUNK_SIZE = 4096;

        public int Count => EncodingData.Count;

        public EncodingHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"encoding\"...");

            stream.Skip(2); // EN
            byte Version = stream.ReadByte(); // must be 1
            byte CKeyLength = stream.ReadByte();
            byte EKeyLength = stream.ReadByte();
            int CKeyPageSize = stream.ReadInt16BE() * 1024; // KB to bytes
            int EKeyPageSize = stream.ReadInt16BE() * 1024; // KB to bytes
            int CKeyPageCount = stream.ReadInt32BE();
            int EKeyPageCount = stream.ReadInt32BE();
            byte unk1 = stream.ReadByte(); // must be 0
            int ESpecBlockSize = stream.ReadInt32BE();

            stream.Skip(ESpecBlockSize);
            //string[] strings = Encoding.ASCII.GetString(stream.ReadBytes(ESpecBlockSize)).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

            stream.Skip(CKeyPageCount * 32);
            //ValueTuple<byte[], byte[]>[] aEntries = new ValueTuple<byte[], byte[]>[CKeyPageCount];

            //for (int i = 0; i < CKeyPageCount; ++i)
            //{
            //    byte[] firstHash = stream.ReadBytes(16);
            //    byte[] blockHash = stream.ReadBytes(16);
            //    aEntries[i] = (firstHash, blockHash);
            //}

            long chunkStart = stream.BaseStream.Position;

            for (int i = 0; i < CKeyPageCount; ++i)
            {
                byte keysCount;

                while ((keysCount = stream.ReadByte()) != 0)
                {
                    long fileSize = stream.ReadInt40BE();
                    MD5Hash cKey = stream.Read<MD5Hash>();

                    EncodingEntry entry = new EncodingEntry()
                    {
                        Size = fileSize
                    };

                    // how do we handle multiple keys?
                    for (int ki = 0; ki < keysCount; ++ki)
                    {
                        MD5Hash eKey = stream.Read<MD5Hash>();

                        // use first key for now
                        if (ki == 0)
                            entry.Key = eKey;
                        //else
                        //    Logger.WriteLine("Multiple encoding keys for MD5 {0}: {1}", md5.ToHexString(), key.ToHexString());

                        //Logger.WriteLine("Encoding {0:D2} {1} {2} {3} {4}", keysCount, aEntries[i].Item1.ToHexString(), aEntries[i].Item2.ToHexString(), md5.ToHexString(), key.ToHexString());
                    }

                    //Encodings[md5] = entry;
                    EncodingData.Add(cKey, entry);
                }

                // each chunk is 4096 bytes, and zero padding at the end
                long remaining = CHUNK_SIZE - ((stream.BaseStream.Position - chunkStart) % CHUNK_SIZE);

                if (remaining > 0)
                    stream.BaseStream.Position += remaining;

                worker?.ReportProgress((int)((i + 1) / (float)CKeyPageCount * 100));
            }

            stream.Skip(EKeyPageCount * 32);
            //for (int i = 0; i < EKeyPageCount; ++i)
            //{
            //    byte[] firstKey = stream.ReadBytes(16);
            //    byte[] blockHash = stream.ReadBytes(16);
            //}

            long chunkStart2 = stream.BaseStream.Position;

            for (int i = 0; i < EKeyPageCount; ++i)
            {
                byte[] eKey = stream.ReadBytes(16);
                int eSpecIndex = stream.ReadInt32BE();
                long fileSize = stream.ReadInt40BE();

                // each chunk is 4096 bytes, and zero padding at the end
                long remaining = CHUNK_SIZE - ((stream.BaseStream.Position - chunkStart2) % CHUNK_SIZE);

                if (remaining > 0)
                    stream.BaseStream.Position += remaining;
            }

            // string block till the end of file

            //EncodingData.Dump();
        }

        public IEnumerable<KeyValuePair<MD5Hash, EncodingEntry>> Entries
        {
            get
            {
                foreach (var entry in EncodingData)
                    yield return entry;
            }
        }

        public bool GetEntry(MD5Hash md5, out EncodingEntry enc) => EncodingData.TryGetValue(md5, out enc);

        public void Clear()
        {
            EncodingData.Clear();
            EncodingData = null;
        }
    }
}
