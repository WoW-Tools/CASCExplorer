using CASCLib;
using System;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class FindByFileDataIdForm : Form
    {
        public FindByFileDataIdForm()
        {
            InitializeComponent();
        }

        public CASCHandler handler { get; set; }

        private void findButton_Click(object sender, EventArgs e)
        {
            if (searchTextBox.Text.Length == 0)
                return;

            int FileDataId = 0;
            try
            {
                FileDataId = int.Parse(searchTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Invalid input", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!handler.FileExists(FileDataId))
            {
                MessageBox.Show("File with this id does not exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var root = handler.Root as WowRootHandler;
            var hash = root.GetHashByFileDataId(FileDataId);

            if (!CASCFile.Files.ContainsKey(hash))
            {
                MessageBox.Show("Hash of file does not exist in file name storage", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            resultTextBox.Text = CASCFile.Files[hash].FullName;
        }
    }
}
