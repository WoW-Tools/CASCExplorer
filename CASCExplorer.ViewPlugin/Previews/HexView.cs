using System;
using System.Windows.Forms;
using System.IO;
using Be.Windows.Forms;
using System.ComponentModel.Composition;
using CASCExplorer.ViewPlugin;
using System.Text;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreview))]
    [ExportMetadata("Extensions", null)]
    public partial class HexView : UserControl, IPreview
    {
        byte[] m_bytes;

        public HexView()
        {
            InitializeComponent();

            cbEncoding.Items.Clear();
            cbEncoding.Items.Add(new DefaultByteCharConverter());
            cbEncoding.Items.Add(new EbcdicByteCharProvider());
            cbEncoding.SelectedIndex = 0;
        }

        public Control Show(Stream stream, string fileName)
        {
            int size = (int)Math.Min(1_000_000, stream.Length);

            m_bytes = new byte[size];
            stream.Read(m_bytes, 0, size);

            hexBox1.ByteProvider = new DynamicByteProvider(m_bytes);

            return this;
        }

        private void cbEncoding_SelectedIndexChanged(object sender, EventArgs e)
        {
            hexBox1.ByteCharConverter = cbEncoding.SelectedItem as IByteCharConverter;
        }
    }
}
