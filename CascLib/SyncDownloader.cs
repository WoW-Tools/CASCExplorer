using System.IO;
using System.Net;
//using System.Net.Http;

namespace CASCExplorer
{
    public class SyncDownloader
    {
        BackgroundWorkerEx progressReporter;

        public SyncDownloader(BackgroundWorkerEx progressReporter)
        {
            this.progressReporter = progressReporter;
        }

        public void DownloadFile(string url, string path)
        {
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

            HttpWebRequest request = WebRequest.CreateHttp(url);

            using (HttpWebResponse resp = (HttpWebResponse)request.GetResponseAsync().Result)
            using (Stream stream = resp.GetResponseStream())
            using (Stream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                CacheMetaData.AddToCache(resp, path);
                CopyToStream(stream, fs, resp.ContentLength);
            }
        }

        public MemoryStream OpenFile(string url)
        {
            HttpWebRequest request = WebRequest.CreateHttp(url);

            using (HttpWebResponse resp = (HttpWebResponse)request.GetResponseAsync().Result)
            using (Stream stream = resp.GetResponseStream())
            {
                MemoryStream ms = new MemoryStream();

                CopyToStream(stream, ms, resp.ContentLength);

                ms.Position = 0;
                return ms;
            }
        }

        public CacheMetaData GetMetaData(string url, string file)
        {
            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(url);
                request.Method = "HEAD";

                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponseAsync().Result)
                {
                    return CacheMetaData.AddToCache(resp, file);
                }
            }
            catch
            {
                return null;
            }
        }

        private void CopyToStream(Stream src, Stream dst, long len)
        {
            long done = 0;

            byte[] buf = new byte[0x1000];

            int count;
            do
            {
                if (progressReporter != null && progressReporter.CancellationPending)
                    return;

                count = src.Read(buf, 0, buf.Length);
                dst.Write(buf, 0, count);

                done += count;

                progressReporter?.ReportProgress((int)(done / (float)len * 100));
            } while (count > 0);
        }
    }
}
