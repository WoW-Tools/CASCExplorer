using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace CASCLib
{
    public class CacheMetaData
    {
        public long Size { get; }
        public string MD5 { get; }

        public CacheMetaData(long size, string md5)
        {
            Size = size;
            MD5 = md5;
        }
    }

    public class CDNCache
    {
        public static bool Enabled { get; set; } = true;
        public static bool CacheData { get; set; } = false;
        public static bool Validate { get; set; } = true;
        public static bool ValidateFast { get; set; } = true;
        public static string CachePath { get; set; } = "cache";

        private readonly MD5 _md5 = MD5.Create();

        private readonly Dictionary<string, Stream> _dataStreams = new Dictionary<string, Stream>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, CacheMetaData> _metaData;

        public CDNCache()
        {
            if (Enabled)
            {
                string metaFile = Path.Combine(CachePath, "cache.meta");

                _metaData = new Dictionary<string, CacheMetaData>(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(metaFile))
                {
                    var lines = File.ReadLines(metaFile);

                    foreach (var line in lines)
                    {
                        string[] tokens = line.Split(' ');
                        _metaData[tokens[0]] = new CacheMetaData(Convert.ToInt64(tokens[1]), tokens[2]);
                    }
                }
            }
        }

        public Stream OpenFile(string name, string url, bool isData)
        {
            if (!Enabled)
                return null;

            if (isData && !CacheData)
                return null;

            string file = Path.Combine(CachePath, name);

            Logger.WriteLine("CDNCache: {0} opening...", file);

            Stream stream = GetDataStream(file, url);

            Logger.WriteLine("CDNCache: {0} has been opened", file);
            numFilesOpened++;

            return stream;
        }

        private Stream GetDataStream(string file, string url)
        {
            string fileName = Path.GetFileName(file);

            if (_dataStreams.TryGetValue(fileName, out Stream stream))
                return stream;

            FileInfo fi = new FileInfo(file);

            if (!fi.Exists)
                DownloadFile(url, file);

            stream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (Validate || ValidateFast)
            {
                if (!_metaData.TryGetValue(fileName, out CacheMetaData meta))
                    meta = GetMetaData(url, fileName);

                if (meta == null)
                    throw new Exception(string.Format("unable to validate file {0}", file));

                bool sizeOk, md5Ok;

                sizeOk = stream.Length == meta.Size;
                md5Ok = ValidateFast || _md5.ComputeHash(stream).ToHexString() == meta.MD5;

                if (sizeOk && md5Ok)
                {
                    _dataStreams.Add(fileName, stream);
                    return stream;
                }
                else
                {
                    stream.Close();
                    _metaData.Remove(fileName);
                    fi.Delete();
                    return GetDataStream(file, url);
                }
            }

            _dataStreams.Add(fileName, stream);
            return stream;
        }

        public CacheMetaData CacheFile(HttpWebResponse resp, string fileName)
        {
            string md5 = resp.Headers[HttpResponseHeader.ETag].Split(':')[0].Substring(1);
            CacheMetaData meta = new CacheMetaData(resp.ContentLength, md5);
            _metaData[fileName] = meta;

            using (var sw = File.AppendText(Path.Combine(CachePath, "cache.meta")))
            {
                sw.WriteLine(string.Format("{0} {1} {2}", fileName, resp.ContentLength, md5.ToUpper()));
            }

            return meta;
        }

        public static TimeSpan timeSpentDownloading = TimeSpan.Zero;
        public static int numFilesOpened = 0;
        public static int numFilesDownloaded = 0;

        public void DownloadFile(string url, string path)
        {
            Logger.WriteLine("CDNCache: downloading file {0} to {1}", url, path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            //using (var client = new HttpClient())
            //{
            //    var msg = client.GetAsync(url).Result;

            //    using (Stream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            //    {
            //        //CacheMetaData.AddToCache(resp, path);
            //        //CopyToStream(stream, fs, resp.ContentLength);

            //        msg.Content.CopyToAsync(fs).Wait();
            //    }
            //}

            DateTime startTime = DateTime.Now;

            HttpWebRequest request = WebRequest.CreateHttp(url);

            using (HttpWebResponse resp = (HttpWebResponse)request.GetResponseAsync().Result)
            using (Stream stream = resp.GetResponseStream())
            using (Stream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                CacheFile(resp, Path.GetFileName(path));
                stream.CopyToStream(fs, resp.ContentLength);
            }

            TimeSpan timeSpent = DateTime.Now - startTime;
            timeSpentDownloading += timeSpent;
            numFilesDownloaded++;

            Logger.WriteLine("CDNCache: {0} has been downloaded, spent {1}", url, timeSpent);
        }

        public CacheMetaData GetMetaData(string url, string fileName)
        {
            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(url);
                request.Method = "HEAD";

                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponseAsync().Result)
                {
                    return CacheFile(resp, fileName);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
