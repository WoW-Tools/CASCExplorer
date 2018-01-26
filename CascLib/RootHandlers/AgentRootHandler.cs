using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCLib
{
    public class AgentRootHandler : RootHandlerBase
    {
        public AgentRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            string hash = Encoding.ASCII.GetString(stream.ReadBytes((int)stream.BaseStream.Length)); // what is this for?

            worker?.ReportProgress(100);
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            yield break;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            yield break;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            yield break;
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {

        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            Logger.WriteLine("AgentRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            Root.Entries.Clear();
            CASCFile.Files.Clear();
        }

        public override void Dump()
        {

        }
    }
}
