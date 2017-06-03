using CASCExplorer.Properties;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class InitForm : Form
    {
        public CASCHandler CASC { get; set; }
        public CASCFolder Root { get; set; }

        private bool _onlineMode;

        public InitForm()
        {
            InitializeComponent();
        }

        private void InitForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.None)
                backgroundWorker1.CancelAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string arg = (string)e.Argument;
            CASCConfig.LoadFlags |= LoadFlags.Install;
            CASCConfig config = _onlineMode ? CASCConfig.LoadOnlineStorageConfig(arg, "us") : CASCConfig.LoadLocalStorageConfig(arg);

            if (_onlineMode)
            {
                using (SelectBuildForm sb = new SelectBuildForm(config))
                {
                    var result = sb.ShowDialog();

                    if (result != DialogResult.OK || sb.SelectedIndex == -1)
                    {
                        e.Cancel = true;
                        return;
                    }

                    config.ActiveBuild = sb.SelectedIndex;
                }
            }

            var casc = CASCHandler.OpenStorage(config, backgroundWorker1);

            casc.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags, false);

            (casc.Root as WowRootHandler)?.LoadFileDataComplete(casc);

            using (var _ = new PerfCounter("LoadListFile()"))
            {
                casc.Root.LoadListFile(Path.Combine(Application.StartupPath, "listfile.txt"), backgroundWorker1);
            }

            var fldr = casc.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);
            casc.Root.MergeInstall(casc.Install);

            GC.Collect();

            e.Result = new object[] { casc, fldr };
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;

            string arg = (string)e.UserState;

            if (arg != null)
                label1.Text = arg;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            if (e.Cancelled)
            {
                MessageBox.Show("Loading cancelled", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //Application.Exit();
                return;
            }

            if (e.Error != null)
            {
                MessageBox.Show("Loading failed due to:\n" + e.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //Application.Exit();
                return;
            }

            if (e.Result == null)
            {
                MessageBox.Show("Loading failed: Result is null", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //Application.Exit();
                return;
            }

            var result = (object[])e.Result;
            CASC = (CASCHandler)result[0];
            Root = (CASCFolder)result[1];

            DialogResult = DialogResult.OK;
        }

        public void LoadLocalStorage(string path)
        {
            _onlineMode = false;
            backgroundWorker1.RunWorkerAsync(path);
        }

        public void LoadOnlineStorage(string product)
        {
            _onlineMode = true;
            backgroundWorker1.RunWorkerAsync(product);
        }
    }
}
