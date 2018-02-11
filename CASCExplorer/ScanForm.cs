using CASCLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class ScanForm : Form
    {
        private static readonly Jenkins96 Hasher = new Jenkins96();

        private FileScanner scanner;
        private HashSet<ScanResult> uniqueFileNames = new HashSet<ScanResult>();

        private CASCHandler CASC;
        private CASCFolder Root;

        private int NumFiles;
        private int NumScanned;
        private bool running;

        private class ScanResult
        {
            public string NewFile { get; set; }
            public string FoundInFile { get; set; }

            public ScanResult(string newFileName, string foundInFileName)
            {
                NewFile = newFileName;
                FoundInFile = foundInFileName;
            }

            public override int GetHashCode()
            {
                return NewFile.ToLower().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                    return false;
                if (!(obj is ScanResult))
                    return false;
                ScanResult res = (ScanResult)obj;
                if (!res.NewFile.ToLower().Equals(NewFile.ToLower()))
                    return false;
                return true;
            }

            public override string ToString()
            {
                return NewFile + " (in: " + FoundInFile + ")";
            }
        }

        public ScanForm()
        {
            InitializeComponent();
        }

        public void Initialize(CASCHandler casc, CASCFolder root)
        {
            CASC = casc;
            Root = root;
            scanner = new FileScanner(CASC, Root);
        }

        public void Reset()
        {
            running = false;
            NumScanned = 0;
            NumFiles = CASC.Root.CountSelect;
            scanButton.Enabled = true;
            scanButton.Text = "Start";
            scanProgressBar.Value = 0;
            scanLabel.Text = "Ready";
            progressLabel.Text = "";
            filenameTextBox.Clear();
            uniqueFileNames.Clear();
        }

        private void UpdateFileNames(string newFileName, string foundInFileName)
        {
            ScanResult res = new ScanResult(newFileName.Replace("/", "\\"), foundInFileName);
            uniqueFileNames.Add(res);
            filenameTextBox.AppendText(res.ToString() + Environment.NewLine);
        }

        private void scanButton_Click(object sender, EventArgs e)
        {
            running = !running;
            if (running)
            {
                Reset();
                running = true;
                scanButton.Text = "Cancel";
                scanBackgroundWorker.RunWorkerAsync();
            }
            else
            {
                scanButton.Enabled = false;
                scanBackgroundWorker.CancelAsync();
            }
        }

        private void scanBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                ScanFolder(Root);
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        private void scanBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            scanProgressBar.Value = e.ProgressPercentage;
            scanLabel.Text = string.Format("Scanning '{0}' ...", e.UserState);
            progressLabel.Text = NumScanned + "/" + NumFiles;
        }

        private void scanBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            running = false;
            scanButton.Text = "Start";
            scanButton.Enabled = true;
            scanLabel.Text = "Scan completed.";
            scanProgressBar.Value = 100;

            // display unique file names without finding place
            filenameTextBox.Clear();

            foreach (var uniqueFileName in uniqueFileNames)
                filenameTextBox.AppendText(uniqueFileName.NewFile + Environment.NewLine);

            if (e.Cancelled)
            {
                Reset();
                scanLabel.Text = "Scan cancelled.";
            }
        }

        private void ScanFolder(CASCFolder folder)
        {
            foreach (var entry in folder.Entries)
            {
                if (entry.Value is CASCFile file)
                    ScanFile(file);
                else
                    ScanFolder(entry.Value as CASCFolder);
            }
        }

        private void ScanFile(CASCFile file)
        {
            if (scanBackgroundWorker.CancellationPending)
                throw new OperationCanceledException();

            NumScanned++;

            var fileNames = scanner.ScanFile(file);

            if (fileNames.Any())
            {
                // only report progress when not skipping a file, it's faster that way
                int progress = (int)(NumScanned / (float)NumFiles * 100);

                scanBackgroundWorker.ReportProgress(progress, file.FullName);

                foreach (var fileName in fileNames)
                {
                    ulong hash = Hasher.ComputeHash(fileName);

                    if ((CASC.Root as WowRootHandler).IsUnknownFile(hash))
                    {
                        BeginInvoke((MethodInvoker)(() => UpdateFileNames(fileName, file.FullName)));
                    }
                }
            }
        }
    }
}
