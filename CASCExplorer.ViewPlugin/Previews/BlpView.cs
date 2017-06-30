using System.Windows.Forms;
using CASCExplorer.ViewPlugin;
using System.IO;
using SereniaBLPLib;
using System.ComponentModel.Composition;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreview))]
    [ExportMetadata("Extensions", new string[] { ".blp" })]
    public partial class BlpView : UserControl, IPreview
    {
        List<Bitmap> m_mips = new List<Bitmap>();
        string m_fileName;

        public BlpView()
        {
            InitializeComponent();
        }

        public Control Show(Stream stream, string fileName)
        {
            m_fileName = fileName;
            m_mips.Clear();
            try
            {
                using (var blp = new BlpFile(stream))
                {
                    for (int i = 0; i < blp.MipMapCount; ++i)
                    {
                        var bmp = blp.GetBitmap(i);
                        if (bmp.Width > 0 && bmp.Height > 0)
                        {
                            m_mips.Add(bmp);
                        }
                    }
                }
            }
            catch// (System.Exception ex)
            {
                //MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            cbMipIndex.Items.Clear();
            cbMipIndex.Items.AddRange(Enumerable.Range(0, m_mips.Count).Select(m => m.ToString()).ToArray());
            if (cbMipIndex.Items.Count > 0)
                cbMipIndex.SelectedIndex = 0;
            return this;
        }

        private void cbMipIndex_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            pictureBox1.Image = m_mips[cbMipIndex.SelectedIndex];
        }

        private void bSave_Click(object sender, System.EventArgs e)
        {
            saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(m_fileName);
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                var format = ImageFormat.Bmp;
                switch (saveFileDialog1.FilterIndex)
                {
                    case 2: format = ImageFormat.Jpeg; break;
                    case 3: format = ImageFormat.Png;  break;
                }
                pictureBox1.Image.Save(saveFileDialog1.FileName, format);
            }
        }
    }
}
