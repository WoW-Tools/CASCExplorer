using System;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public sealed class CASCHandler : CASCHandlerBase
    {
        private EncodingHandler EncodingHandler;
        private DownloadHandler DownloadHandler;
        private RootHandlerBase RootHandler;
        private InstallHandler InstallHandler;

        public EncodingHandler Encoding => EncodingHandler;
        public DownloadHandler Download => DownloadHandler;
        public RootHandlerBase Root => RootHandler;
        public InstallHandler Install => InstallHandler;

        private CASCHandler(CASCConfig config, BackgroundWorkerEx worker) : base(config, worker)
        {
            Logger.WriteLine("CASCHandler: loading encoding data...");

            using (var _ = new PerfCounter("new EncodingHandler()"))
            {
                using (var fs = OpenEncodingFile(this))
                    EncodingHandler = new EncodingHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingHandler.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Download) != 0)
            {
                Logger.WriteLine("CASCHandler: loading download data...");

                using (var _ = new PerfCounter("new DownloadHandler()"))
                {
                    using (var fs = OpenDownloadFile(EncodingHandler, this))
                        DownloadHandler = new DownloadHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} download data", EncodingHandler.Count);
            }

            Logger.WriteLine("CASCHandler: loading root data...");

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                using (var fs = OpenRootFile(EncodingHandler, this))
                {
                    if (config.GameType == CASCGameType.S2 || config.GameType == CASCGameType.HotS)
                        RootHandler = new MNDXRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.D3)
                        RootHandler = new D3RootHandler(fs, worker, this);
                    else if (config.GameType == CASCGameType.WoW)
                        RootHandler = new WowRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Agent || config.GameType == CASCGameType.Bna || config.GameType == CASCGameType.Client)
                        RootHandler = new AgentRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Hearthstone)
                        RootHandler = new HSRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Overwatch)
                        RootHandler = new OWRootHandler(fs, worker, this);
                    else
                        throw new Exception("Unsupported game " + config.BuildUID);
                }
            }

            Logger.WriteLine("CASCHandler: loaded {0} root data", RootHandler.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                Logger.WriteLine("CASCHandler: loading install data...");

                using (var _ = new PerfCounter("new InstallHandler()"))
                {
                    using (var fs = OpenInstallFile(EncodingHandler, this))
                        InstallHandler = new InstallHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} install data", InstallHandler.Count);
            }
        }

        public static CASCHandler OpenStorage(CASCConfig config, BackgroundWorkerEx worker = null) => Open(worker, config);

        public static CASCHandler OpenLocalStorage(string basePath, BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadLocalStorageConfig(basePath);

            return Open(worker, config);
        }

        public static CASCHandler OpenOnlineStorage(string product, string region = "us", BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product, region);

            return Open(worker, config);
        }

        private static CASCHandler Open(BackgroundWorkerEx worker, CASCConfig config)
        {
            using (var _ = new PerfCounter("new CASCHandler()"))
            {
                return new CASCHandler(config, worker);
            }
        }

        public override bool FileExists(int fileDataId)
        {
            WowRootHandler rh = Root as WowRootHandler;

            if (rh == null)
                return false;

            return FileExists(rh.GetHashByFileDataId(fileDataId));
        }

        public override bool FileExists(string file) => FileExists(Hasher.ComputeHash(file));

        public override bool FileExists(ulong hash) => RootHandler.GetAllEntries(hash).Any();

        public bool GetEncodingEntry(ulong hash, out EncodingEntry enc)
        {
            var rootInfos = RootHandler.GetEntries(hash);
            if (rootInfos.Any())
                return EncodingHandler.GetEntry(rootInfos.First().MD5, out enc);

            if ((CASCConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                var installInfos = Install.GetEntries().Where(e => Hasher.ComputeHash(e.Name) == hash);
                if (installInfos.Any())
                    return EncodingHandler.GetEntry(installInfos.First().MD5, out enc);
            }

            enc = default(EncodingEntry);
            return false;
        }

        public override Stream OpenFile(int fileDataId)
        {
            WowRootHandler rh = Root as WowRootHandler;

            if (rh != null)
                return OpenFile(rh.GetHashByFileDataId(fileDataId));

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException("FileData: " + fileDataId.ToString());
            return null;
        }

        public override Stream OpenFile(string name) => OpenFile(Hasher.ComputeHash(name));

        public override Stream OpenFile(ulong hash)
        {
            EncodingEntry encInfo;

            if (GetEncodingEntry(hash, out encInfo))
                return OpenFile(encInfo.Key);

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException(string.Format("{0:X16}", hash));
            return null;
        }

        public override void SaveFileTo(ulong hash, string extractPath, string fullName)
        {
            EncodingEntry encInfo;

            if (GetEncodingEntry(hash, out encInfo))
            {
                SaveFileTo(encInfo.Key, extractPath, fullName);
                return;
            }

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException(fullName);
        }

        protected override Stream OpenFileOnline(MD5Hash key)
        {
            IndexEntry idxInfo = CDNIndex.GetIndexInfo(key);
            return OpenFileLocalInternal(idxInfo, key);
        }

        protected override Stream GetLocalDataStream(MD5Hash key)
        {
            IndexEntry idxInfo = LocalIndex.GetIndexInfo(key);
            return GetLocalDataStreamInternal(idxInfo, key);
        }

        protected override void ExtractFileOnline(MD5Hash key, string path, string name)
        {
            IndexEntry idxInfo = CDNIndex.GetIndexInfo(key);
            ExtractFileOnlineInternal(idxInfo, key, path, name);
        }

        public void Clear()
        {
            CDNIndex?.Clear();
            CDNIndex = null;

            foreach (var stream in DataStreams)
                stream.Value.Close();

            DataStreams.Clear();

            EncodingHandler?.Clear();
            EncodingHandler = null;

            InstallHandler?.Clear();
            InstallHandler = null;

            LocalIndex?.Clear();
            LocalIndex = null;

            RootHandler?.Clear();
            RootHandler = null;

            DownloadHandler?.Clear();
            DownloadHandler = null;
        }
    }
}
