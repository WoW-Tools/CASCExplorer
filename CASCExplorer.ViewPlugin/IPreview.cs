using System.IO;
using System.Windows.Forms;

namespace CASCExplorer.ViewPlugin
{
    public interface IExtensions
    {
        string[] Extensions { get; }
    }

    public interface IPreview
    {
        Control Show(Stream stream, string fileName);
    }
}
