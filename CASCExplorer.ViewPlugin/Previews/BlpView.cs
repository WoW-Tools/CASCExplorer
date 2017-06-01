using System.Windows.Forms;
using CASCExplorer.ViewPlugin;
using System.IO;
using SereniaBLPLib;
using System.ComponentModel.Composition;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreviw))]
    public partial class BlpView : UserControl, IPreviw
    {
        public BlpView()
        {
            InitializeComponent();
        }

        public bool CheckContent(string extension)
        {
            return extension == ".blp";
        }

        public Control Show(Stream stream)
        {
            using (var blp = new BlpFile(stream))
                pictureBox1.Image = blp.GetBitmap(0);
            return this;
        }
    }
}
