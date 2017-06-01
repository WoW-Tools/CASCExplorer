using System.Linq;
using System.Text;
using System.Windows.Forms;
using CASCExplorer.ViewPlugin;
using System.IO;
using System.ComponentModel.Composition;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreviw))]
    public partial class TextView : UserControl, IPreviw
    {
        static string[] m_extensions = { ".txt", ".ini", ".wtf", ".lua", ".toc", ".xml", ".htm", ".html", ".lst" };

        public TextView()
        {
            InitializeComponent();
        }

        public bool CheckContent(string extension)
        {
            return m_extensions.Contains(extension);
        }

        public Control Show(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                richTextBox1.Text = reader.ReadToEnd();
            return this;
        }
    }
}
