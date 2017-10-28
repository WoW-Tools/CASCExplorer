using CASCLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class ExtractProgress : Form
    {
        private string ExtractPath
        {
            get => textBox1.Text;
            set => textBox1.Text = value;
        }

        private int NumExtracted;
        private ICollection<CASCFile> files;
        private CASCHandler CASC;

        public ExtractProgress()
        {
            InitializeComponent();
        }

        public void SetExtractData(CASCHandler handler, ICollection<CASCFile> files)
        {
            NumExtracted = 0;
            progressBar1.Value = 0;
            CASC = handler;
            this.files = files;
            ExtractPath = ExtractPath;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();

            if (result != DialogResult.OK)
                return;

            var path = folderBrowserDialog1.SelectedPath;

            if (path == null)
                return;

            ExtractPath = path;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy)
            {
                backgroundWorker1.CancelAsync();
                return;
            }

            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            textBox1.ReadOnly = true;
            backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            foreach (var file in files)
            {
                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }

                try
                {
                    backgroundWorker1.ReportProgress((int)((float)++NumExtracted / (float)files.Count * 100));

                    CASC.SaveFileTo(file.Hash, ExtractPath, file.FullName);
                }
                catch (Exception exc)
                {
                    Logger.WriteLine("Unable to extract file {0}: {1}", file.FullName, exc.Message);
                }
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button2.Enabled = true;
            textBox1.ReadOnly = false;

            if (e.Cancelled)
            {
                NumExtracted = 0;
                progressBar1.Value = 0;
                MessageBox.Show("Operation cancelled!");
                return;
            }

            Hide();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            button2.Enabled = Directory.Exists(ExtractPath);
        }
    }
}
