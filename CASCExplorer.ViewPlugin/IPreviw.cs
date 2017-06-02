using System.IO;
using System.Windows.Forms;

namespace CASCExplorer.ViewPlugin
{
    public interface IExtensions
    {
        string[] Extensions { get; }
    }

    public interface IPreviw
    {
        Control Show(Stream stream, string fileName);
    }
}
