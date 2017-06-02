using System;
using System.Windows.Forms;
using System.IO;
using Be.Windows.Forms;
using System.ComponentModel.Composition;
using CASCExplorer.ViewPlugin;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreviwDefault))]
    public partial class HexView : UserControl, IPreviwDefault
    {
        byte[] m_bytes;

        public HexView()
        {
            InitializeComponent();
        }

        public Control Show(Stream stream, string fileName)
        {
            int size = (int)Math.Min(1_000_000, stream.Length);

            m_bytes = new byte[size];
            stream.Read(m_bytes, 0, size);

            hexBox1.ByteProvider = new DynamicByteProvider(m_bytes);

            return this;
        }
    }
}
