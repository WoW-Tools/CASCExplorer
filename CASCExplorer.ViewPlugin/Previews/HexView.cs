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
        public HexView()
        {
            InitializeComponent();
        }

        public Control Show(Stream stream)
        {
            int size = (int)Math.Min(1_000_000, stream.Length);
            var bytes = new byte[size];
            stream.Read(bytes, 0, size);
            hexBox1.ByteProvider = new DynamicByteProvider(bytes);

            return this;
        }
    }
}
