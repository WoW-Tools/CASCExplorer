using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
//using System.Net.Http;
//using System.Net.Http.Headers;

namespace CASCExplorer
{
    public class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CDNIndexHandler
    {
        private static readonly MD5HashComparer comparer = new MD5HashComparer();
        private Dictionary<MD5Hash, IndexEntry> CDNIndexData = new Dictionary<MD5Hash, IndexEntry>(comparer);

        private CASCConfig config;
        private BackgroundWorkerEx worker;
        private SyncDownloader downloader;
        public static readonly CDNCache Cache = new CDNCache("cache");

        public int Count => CDNIndexData.Count;

        private CDNIndexHandler(CASCConfig cascConfig, BackgroundWorkerEx worker)
        {
            config = cascConfig;
            this.worker = worker;
            downloader = new SyncDownloader(worker);
        }

        public static CDNIndexHandler Initialize(CASCConfig config, BackgroundWorkerEx worker)
        {
            var handler = new CDNIndexHandler(config, worker);

            worker?.ReportProgress(0, "Loading \"CDN indexes\"...");

            for (int i = 0; i < config.Archives.Count; i++)
            {
                string archive = config.Archives[i];

                if (config.OnlineMode)
                    handler.DownloadIndexFile(archive, i);
                else
                    handler.OpenIndexFile(archive, i);

                worker?.ReportProgress((int)((i + 1) / (float)config.Archives.Count * 100));
            }

            return handler;
        }

        private void ParseIndex(Stream stream, int i)
        {
            using (var br = new BinaryReader(stream))
            {
                stream.Seek(-12, SeekOrigin.End);
                int count = br.ReadInt32();
                stream.Seek(0, SeekOrigin.Begin);

                if (count * (16 + 4 + 4) > stream.Length)
                    throw new Exception("ParseIndex failed");

                for (int j = 0; j < count; ++j)
                {
                    MD5Hash key = br.Read<MD5Hash>();

                    if (key.IsZeroed()) // wtf?
                        key = br.Read<MD5Hash>();

                    if (key.IsZeroed()) // wtf?
                        throw new Exception("key.IsZeroed()");

                    IndexEntry entry = new IndexEntry()
                    {
                        Index = i,
                        Size = br.ReadInt32BE(),
                        Offset = br.ReadInt32BE()
                    };
                    CDNIndexData.Add(key, entry);
                }
            }
        }

        private void DownloadIndexFile(string archive, int i)
        {
            try
            {
                string file = config.CDNPath + "/data/" + archive.Substring(0, 2) + "/" + archive.Substring(2, 2) + "/" + archive + ".index";
                string url = "http://" + config.CDNHost + "/" + file;

                Stream stream = Cache.OpenFile(file, url, false);

                if (stream != null)
                {
                    ParseIndex(stream, i);
                }
                else
                {
                    using (var fs = downloader.OpenFile(url))
                        ParseIndex(fs, i);
                }
            }
            catch
            {
                throw new Exception("DownloadFile failed!");
            }
        }

        private void OpenIndexFile(string archive, int i)
        {
            try
            {
                string dataFolder = CASCGame.GetDataFolder(config.GameType);

                string path = Path.Combine(config.BasePath, dataFolder, "indices", archive + ".index");

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    ParseIndex(fs, i);
            }
            catch
            {
                throw new Exception("OpenFile failed!");
            }
        }

        public Stream OpenDataFile(IndexEntry entry)
        {
            var archive = config.Archives[entry.Index];

            string file = config.CDNPath + "/data/" + archive.Substring(0, 2) + "/" + archive.Substring(2, 2) + "/" + archive;
            string url = "http://" + config.CDNHost + "/" + file;

            Stream stream = Cache.OpenFile(file, url, true);

            if (stream != null)
            {
                stream.Position = entry.Offset;
                MemoryStream ms = new MemoryStream(entry.Size);
                stream.CopyBytes(ms, entry.Size);
                ms.Position = 0;
                return ms;
            }

            //using (HttpClient client = new HttpClient())
            //{
            //    client.DefaultRequestHeaders.Range = new RangeHeaderValue(entry.Offset, entry.Offset + entry.Size - 1);

            //    var resp = client.GetStreamAsync(url).Result;

            //    MemoryStream ms = new MemoryStream(entry.Size);
            //    resp.CopyBytes(ms, entry.Size);
            //    ms.Position = 0;
            //    return ms;
            //}

            HttpWebRequest req = WebRequest.CreateHttp(url);
            //req.Headers[HttpRequestHeader.Range] = string.Format("bytes={0}-{1}", entry.Offset, entry.Offset + entry.Size - 1);
            req.AddRange(entry.Offset, entry.Offset + entry.Size - 1);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponseAsync().Result)
            {
                MemoryStream ms = new MemoryStream(entry.Size);
                resp.GetResponseStream().CopyBytes(ms, entry.Size);
                ms.Position = 0;
                return ms;
            }
        }

        public Stream OpenDataFileDirect(MD5Hash key)
        {
            var keyStr = key.ToHexString().ToLower();

            worker?.ReportProgress(0, string.Format("Downloading \"{0}\" file...", keyStr));

            string file = config.CDNPath + "/data/" + keyStr.Substring(0, 2) + "/" + keyStr.Substring(2, 2) + "/" + keyStr;
            string url = "http://" + config.CDNHost + "/" + file;

            Stream stream = Cache.OpenFile(file, url, false);

            if (stream != null)
                return stream;

            return downloader.OpenFile(url);
        }

        public static Stream OpenConfigFileDirect(CASCConfig cfg, string key)
        {
            string file = cfg.CDNPath + "/config/" + key.Substring(0, 2) + "/" + key.Substring(2, 2) + "/" + key;
            string url = "http://" + cfg.CDNHost + "/" + file;

            Stream stream = Cache.OpenFile(file, url, false);

            if (stream != null)
                return stream;

            return OpenFileDirect(url);
        }

        public static Stream OpenFileDirect(string url)
        {
            //using (HttpClient client = new HttpClient())
            //{
            //    var resp = client.GetStreamAsync(url).Result;

            //    MemoryStream ms = new MemoryStream();
            //    resp.CopyTo(ms);
            //    ms.Position = 0;
            //    return ms;
            //}

            HttpWebRequest req = WebRequest.CreateHttp(url);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponseAsync().Result)
            {
                MemoryStream ms = new MemoryStream();
                resp.GetResponseStream().CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        public IndexEntry GetIndexInfo(MD5Hash key)
        {
            if (!CDNIndexData.TryGetValue(key, out IndexEntry result))
                Logger.WriteLine("CDNIndexHandler: missing index: {0}", key.ToHexString());

            return result;
        }

        public void Clear()
        {
            CDNIndexData.Clear();
            CDNIndexData = null;

            config = null;
            worker = null;
            downloader = null;
        }
    }
}
