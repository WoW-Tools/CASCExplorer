using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCLib
{
    public class LocalIndexHandler
    {
        private static readonly MD5HashComparer comparer = new MD5HashComparer();
        private Dictionary<MD5Hash, IndexEntry> LocalIndexData = new Dictionary<MD5Hash, IndexEntry>(comparer);

        public int Count
        {
            get { return LocalIndexData.Count; }
        }

        private LocalIndexHandler()
        {

        }

        public static LocalIndexHandler Initialize(CASCConfig config, BackgroundWorkerEx worker)
        {
            var handler = new LocalIndexHandler();

            var idxFiles = GetIdxFiles(config);

            if (idxFiles.Count == 0)
                throw new FileNotFoundException("idx files missing!");

            worker?.ReportProgress(0, "Loading \"local indexes\"...");

            int idxIndex = 0;

            foreach (var idx in idxFiles)
            {
                handler.ParseIndex(idx);

                worker?.ReportProgress((int)(++idxIndex / (float)idxFiles.Count * 100));
            }

            Logger.WriteLine("LocalIndexHandler: loaded {0} indexes", handler.Count);

            return handler;
        }

        private unsafe void ParseIndex(string idx)
        {
            using (var fs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                int h2Len = br.ReadInt32();
                int h2Check = br.ReadInt32();
                byte[] h2 = br.ReadBytes(h2Len);

                long padPos = (8 + h2Len + 0x0F) & 0xFFFFFFF0;
                fs.Position = padPos;

                int dataLen = br.ReadInt32();
                int dataCheck = br.ReadInt32();

                int numBlocks = dataLen / 18;

                //byte[] buf = new byte[8];

                for (int i = 0; i < numBlocks; i++)
                {
                    IndexEntry info = new IndexEntry();
                    byte[] keyBytes = br.ReadBytes(9);
                    Array.Resize(ref keyBytes, 16);

                    MD5Hash key;

                    fixed (byte *ptr = keyBytes)
                        key = *(MD5Hash*)ptr;

                    byte indexHigh = br.ReadByte();
                    int indexLow = br.ReadInt32BE();

                    info.Index = (indexHigh << 2 | (byte)((indexLow & 0xC0000000) >> 30));
                    info.Offset = (indexLow & 0x3FFFFFFF);

                    //for (int j = 3; j < 8; j++)
                    //    buf[7 - j] = br.ReadByte();

                    //long val = BitConverter.ToInt64(buf, 0);
                    //info.Index = (int)(val / 0x40000000);
                    //info.Offset = (int)(val % 0x40000000);

                    info.Size = br.ReadInt32();

                    // duplicate keys wtf...
                    //IndexData[key] = info; // use last key
                    if (!LocalIndexData.ContainsKey(key)) // use first key
                        LocalIndexData.Add(key, info);
                }

                padPos = (dataLen + 0x0FFF) & 0xFFFFF000;
                fs.Position = padPos;

                fs.Position += numBlocks * 18;
                //for (int i = 0; i < numBlocks; i++)
                //{
                //    var bytes = br.ReadBytes(18); // unknown data
                //}

                //if (fs.Position != fs.Length)
                //    throw new Exception("idx file under read");
            }
        }

        private static List<string> GetIdxFiles(CASCConfig config)
        {
            List<string> latestIdx = new List<string>();

            string dataFolder = CASCGame.GetDataFolder(config.GameType);
            string dataPath = Path.Combine(dataFolder, "data");

            for (int i = 0; i < 0x10; ++i)
            {
                var files = Directory.EnumerateFiles(Path.Combine(config.BasePath, dataPath), string.Format("{0:x2}*.idx", i));

                if (files.Count() > 0)
                    latestIdx.Add(files.Last());
            }

            return latestIdx;
        }

        public unsafe IndexEntry GetIndexInfo(MD5Hash key)
        {
            ulong* ptr = (ulong*)&key;
            ptr[1] &= 0xFF;

            if (!LocalIndexData.TryGetValue(key, out IndexEntry result))
                Logger.WriteLine("LocalIndexHandler: missing index: {0}", key.ToHexString());

            return result;
        }

        public void Clear()
        {
            LocalIndexData.Clear();
            LocalIndexData = null;
        }
    }
}
