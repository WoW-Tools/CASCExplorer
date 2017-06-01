using System.IO;
using System.Windows.Forms;

namespace CASCExplorer.ViewPlugin
{
    public interface IPreviwDefault
    {
        Control Show(Stream stream);
    }

    public interface IPreviw : IPreviwDefault
    {
        bool CheckContent(string extension);
    }
}
