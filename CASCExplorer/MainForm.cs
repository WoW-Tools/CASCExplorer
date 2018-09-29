using CASCExplorer.Properties;
using CASCLib;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class MainForm : Form
    {
        private SearchForm searchForm;
        private CASCViewHelper viewHelper = new CASCViewHelper();

        public MainForm()
        {
            InitializeComponent();
            viewHelper.ViewPanel = splitContainer2.Panel2;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            viewHelper.OnCleanup += ViewHelper_OnCleanup;
            viewHelper.OnStorageChanged += ViewHelper_OnStorageChanged;

            Settings.Default.PropertyChanged += Settings_PropertyChanged;

            iconsList.Images.Add(Resources.folder);
            iconsList.Images.Add(Resources.openFolder);
            iconsList.Images.Add(SystemIcons.WinLogo);

            folderTree.SelectedImageIndex = 1;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var locales = Enum.GetNames(typeof(LocaleFlags));
            foreach (var locale in locales)
            {
                if (locale == "None")
                    continue;

                var item = new ToolStripMenuItem(locale)
                {
                    Checked = Settings.Default.LocaleFlags.ToString() == locale
                };
                localeFlagsToolStripMenuItem.DropDownItems.Add(item);
            }

            NameValueCollection onlineStorageList = (NameValueCollection)ConfigurationManager.GetSection("OnlineStorageList");

            if (onlineStorageList != null)
            {
                openOnlineStorageToolStripMenuItem.Enabled = onlineStorageList.Count > 0;

                foreach (string game in onlineStorageList)
                {
                    var item = new ToolStripMenuItem(onlineStorageList[game])
                    {
                        Tag = game
                    };
                    openOnlineStorageToolStripMenuItem.DropDownItems.Add(item);
                }
            }

            openRecentStorageToolStripMenuItem.Enabled = Settings.Default.RecentStorages.Count > 0;

            foreach (string recentStorage in Settings.Default.RecentStorages)
            {
                openRecentStorageToolStripMenuItem.DropDownItems.Add(recentStorage);
            }

            useLVToolStripMenuItem.Checked = (Settings.Default.ContentFlags & ContentFlags.LowViolence) != 0;
            tsmShowPreview.Checked = Settings.Default.PreviewVisible;
        }

        private void ViewHelper_OnStorageChanged()
        {
            OnStorageChanged();
        }

        private void ViewHelper_OnCleanup()
        {
            Cleanup();
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Settings.Default.Save();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        private void OnStorageChanged()
        {
            CASCHandler casc = viewHelper.CASC;
            CASCConfig cfg = casc.Config;
            CASCGameType gameType = cfg.GameType;

            bool isWoW = gameType == CASCGameType.WoW;

            extractInstallFilesToolStripMenuItem.Enabled = true;
            extractCASCSystemFilesToolStripMenuItem.Enabled = true;
            scanFilesToolStripMenuItem.Enabled = isWoW;
            analyseUnknownFilesToolStripMenuItem.Enabled = isWoW || gameType == CASCGameType.Overwatch;
            addFileDataIDToSoundFilesToolStripMenuItem.Enabled = isWoW;
            analyzeSoundFilesToolStripMenuItem.Enabled = isWoW;
            localeFlagsToolStripMenuItem.Enabled = CASCGame.SupportsLocaleSelection(gameType);
            useLVToolStripMenuItem.Enabled = isWoW;
            exportListfileToolStripMenuItem.Enabled = true;

            CASCFolder root = viewHelper.Root;

            TreeNode node = new TreeNode() { Name = root.Name, Tag = root, Text = "Root [Read only]" };
            folderTree.Nodes.Add(node);
            node.Nodes.Add(new TreeNode() { Name = "tempnode" }); // add dummy node
            node.Expand();
            folderTree.SelectedNode = node;

            if (cfg.OnlineMode)
            {
                CDNBuildsToolStripMenuItem.Enabled = true;
                if (cfg.Builds.Count > 1)
                {
                    foreach (var bld in cfg.Builds)
                    {
                        CDNBuildsToolStripMenuItem.DropDownItems.Add(bld["build-name"][0]);
                    }
                }
                else
                    CDNBuildsToolStripMenuItem.DropDownItems.Add(cfg.BuildName);
            }

            statusProgress.Visible = false;
            statusLabel.Text = string.Format("Loaded {0} files ({1} names missing)", casc.Root.CountSelect - casc.Root.CountUnknown, casc.Root.CountUnknown);
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            viewHelper.UpdateListView(e.Node.Tag as CASCFolder, fileList, filterToolStripTextBox.Text);

            statusLabel.Text = e.Node.FullPath;
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            viewHelper.CreateTreeNodes(e.Node);
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            viewHelper.SetSort(e.Column);

            CASCFolder folder = viewHelper.CurrentFolder;

            if (folder == null)
                return;

            viewHelper.UpdateListView(folder, fileList, filterToolStripTextBox.Text);
        }

        private void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            viewHelper.CreateListViewItem(e);
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            viewHelper.PreviewFile(fileList);
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!NavigateFolder())
                viewHelper.PreviewFile(fileList);
        }

        private void listView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.A | Keys.Control))
            {
                for (int i = 0; i < fileList.VirtualListSize; i++)
                    fileList.Items[i].Selected = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
                NavigateFolder();
            else if (e.KeyCode == Keys.Back)
            {
                TreeNode node = folderTree.SelectedNode;
                if (node != null && node != folderTree.Nodes["root"])
                    folderTree.SelectedNode = node.Parent;
            }
        }

        private bool NavigateFolder()
        {
            // Current folder
            CASCFolder folder = viewHelper.CurrentFolder;

            if (folder == null)
                return false;

            if (!fileList.HasSingleSelection)
                return false;

            // Selected folder
            CASCFolder baseEntry = viewHelper.DisplayedEntries[fileList.SelectedIndex] as CASCFolder;

            if (baseEntry == null)
                return false;

            folderTree.SelectedNode.Expand();
            folderTree.SelectedNode.Nodes[baseEntry.Name].Expand();
            folderTree.SelectedNode = folderTree.SelectedNode.Nodes[baseEntry.Name];

            viewHelper.UpdateListView(baseEntry, fileList, filterToolStripTextBox.Text);

            statusLabel.Text = folderTree.SelectedNode.FullPath;
            return true;
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.ExtractFiles(fileList);
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            extractToolStripMenuItem.Enabled = fileList.HasSelection;
            copyNameToolStripMenuItem.Enabled = (fileList.HasSelection && CASCFolder.GetFiles(viewHelper.DisplayedEntries, fileList.SelectedIndices.Cast<int>(), false).Count() > 0) || false;
            getSizeToolStripMenuItem.Enabled = fileList.HasSelection;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutBox about = new AboutBox())
                about.ShowDialog();
        }

        private void copyNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CASCFolder folder = viewHelper.CurrentFolder;

            if (folder == null)
                return;

            if (!fileList.HasSelection)
                return;

            var files = CASCFolder.GetFiles(viewHelper.DisplayedEntries, fileList.SelectedIndices.Cast<int>(), false).Select(f => f.FullName);

            string temp = string.Join(Environment.NewLine, files);

            Clipboard.SetText(temp);
        }

        private void scanFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.ScanFiles();
        }

        private async void analyseUnknownFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                statusProgress.Value = 0;
                statusProgress.Visible = true;
                analyseUnknownFilesToolStripMenuItem.Enabled = false;

                statusLabel.Text = "Analysing...";

                await viewHelper.AnalyzeUnknownFiles((p) => statusProgress.Value = p);

                statusLabel.Text = "All unknown files has been analyzed.";
            }
            catch (Exception exc)
            {
                statusLabel.Text = "Failed to analyze unknown files.";
                MessageBox.Show("Error during analysis of unknown files:\n" + exc.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusProgress.Value = 0;
                statusProgress.Visible = false;
                analyseUnknownFilesToolStripMenuItem.Enabled = true;
            }
        }

        private void localeToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var item = e.ClickedItem as ToolStripMenuItem;

            var parent = (sender as ToolStripMenuItem);

            foreach (var dropdown in parent.DropDownItems)
            {
                if (dropdown != item)
                    (dropdown as ToolStripMenuItem).Checked = false;
                else
                    (dropdown as ToolStripMenuItem).Checked = true;
            }

            viewHelper.ChangeLocale(item.Text);
        }

        private void getSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.GetSize(fileList);
        }

        private void contentFlagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            useLVToolStripMenuItem.Checked = !useLVToolStripMenuItem.Checked;

            viewHelper.ChangeContentFlags(useLVToolStripMenuItem.Checked);
        }

        private void Cleanup()
        {
            fileList.VirtualListSize = 0;

            if (folderTree.Nodes.Count > 0)
            {
                folderTree.Nodes[0].Tag = null;
            }

            folderTree.Nodes.Clear();

            CDNBuildsToolStripMenuItem.Enabled = false;
            CDNBuildsToolStripMenuItem.DropDownItems.Clear();

            extractInstallFilesToolStripMenuItem.Enabled = false;
            extractCASCSystemFilesToolStripMenuItem.Enabled = false;
            scanFilesToolStripMenuItem.Enabled = false;
            analyseUnknownFilesToolStripMenuItem.Enabled = false;
            localeFlagsToolStripMenuItem.Enabled = false;
            useLVToolStripMenuItem.Enabled = false;
            exportListfileToolStripMenuItem.Enabled = false;
            statusLabel.Text = "Ready.";
            statusProgress.Visible = false;

            GC.Collect();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (searchForm == null)
                searchForm = new SearchForm(fileList);

            if (!searchForm.Visible)
                searchForm.Show(this);
        }

        private void fileList_SearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            viewHelper.Search(fileList, e);
        }

        private async void extractInstallFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                statusProgress.Value = 0;
                statusProgress.Visible = true;
                extractInstallFilesToolStripMenuItem.Enabled = false;

                statusLabel.Text = "Extracting...";

                await viewHelper.ExtractInstallFiles((p) => statusProgress.Value = p);

                statusLabel.Text = "All install files has been extracted.";
            }
            catch (Exception exc)
            {
                statusLabel.Text = "Failed to extract install files.";
                MessageBox.Show("Error during extraction of install files:\n" + exc.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusProgress.Value = 0;
                statusProgress.Visible = false;
                extractInstallFilesToolStripMenuItem.Enabled = true;
            }
        }

        private void extractCASCSystemFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.ExtractCASCSystemFiles();
        }

        private void bruteforceNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (BruteforceForm bf = new BruteforceForm())
                bf.ShowDialog();
        }

        private void openStorageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenStorage();
        }

        private void OpenStorage()
        {
            if (storageFolderBrowserDialog.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("Please select storage folder!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string path = storageFolderBrowserDialog.SelectedPath;

            if (!File.Exists(Path.Combine(path, ".build.info")))
            {
                MessageBox.Show("Invalid storage folder selected!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            viewHelper.OpenStorage(path, false);

            openRecentStorageToolStripMenuItem.Enabled = true;
            openRecentStorageToolStripMenuItem.DropDownItems.Add(path);

            StringCollection recentStorages = Settings.Default.RecentStorages;
            if (!recentStorages.Contains(path))
                recentStorages.Add(path);
            Settings.Default.RecentStorages = recentStorages;
        }

        private void openOnlineStorageToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            viewHelper.OpenStorage((string)e.ClickedItem.Tag, true);
        }

        private void openRecentStorageToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            viewHelper.OpenStorage(e.ClickedItem.Text, false);
        }

        private void closeStorageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.Cleanup();
        }

        private void exportListfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.ExportListFile();
        }

        private void filterToolStripTextBox_TextChanged(object sender, EventArgs e)
        {
            viewHelper.UpdateListView(viewHelper.CurrentFolder, fileList, filterToolStripTextBox.Text);
        }

        private void openStorageToolStripButton_Click(object sender, EventArgs e)
        {
            OpenStorage();
        }

        private void exportFoldersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewHelper.ExportFolders();
        }

        private void tsmShowPreview_CheckedChanged(object sender, EventArgs e)
        {
            splitContainer2.Panel2Collapsed = !tsmShowPreview.Checked;
            viewHelper.ViewPanel = tsmShowPreview.Checked ? splitContainer2.Panel2 : null;
            Settings.Default.PreviewVisible = tsmShowPreview.Checked;
        }

        private void openListFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Text files(*.txt)|*.txt";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.ListFilePath = dialog.FileName;
                    Settings.Default.Save();
                    viewHelper?.CASC?.Root?.LoadListFile(Settings.Default.ListFilePath);
                }
            }
        }

        private void analyzeSoundFilesToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            viewHelper.AnalyzeSoundFiles = analyzeSoundFilesToolStripMenuItem.Checked;
        }

        private void addFileDataIDToSoundFilesToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            viewHelper.AddFileDataIdToSoundFiles = addFileDataIDToSoundFilesToolStripMenuItem.Checked;
        }
    }
}
