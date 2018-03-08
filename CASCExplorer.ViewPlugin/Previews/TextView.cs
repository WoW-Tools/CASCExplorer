using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CASCExplorer.ViewPlugin;
using System.IO;
using System.ComponentModel.Composition;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreview))]
    [ExportMetadata("Extensions", new string[] { ".txt", ".ini", ".wtf", ".lua", ".toc", ".xml", ".htm", ".html", ".lst", ".signed", ".anim", ".plist" })]
    public partial class TextView : UserControl, IPreview
    {
        byte[] m_bytes;

        public TextView()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;
        }

        private void GetText()
        {
            richTextBox1.Clear();
            if (m_bytes != null)
            {
                var enc_name = (string)comboBox1.SelectedItem ?? "utf-8";
                var enc = Encoding.GetEncoding(enc_name);
                richTextBox1.Text = enc.GetString(m_bytes);
            }
        }

        private void GetAnim()
        {
            richTextBox1.Clear();
            if (m_bytes?.Length > 8)
            {
                var sig = BitConverter.ToUInt64(m_bytes, 0);
                var text = Encoding.UTF8.GetString(m_bytes, 8, m_bytes.Length - 8);

                richTextBox1.AppendText($"Signature: 0x{sig:X08}");
                richTextBox1.AppendText(Environment.NewLine);
                richTextBox1.AppendText(Environment.NewLine);
                richTextBox1.AppendText(text);

                richTextBox1.WordWrap = true;
            }
        }

        public Control Show(Stream stream, string fileName)
        {
            m_bytes = new byte[stream.Length];
            stream.Read(m_bytes, 0, (int)stream.Length);
            if (fileName.EndsWith(".anim"))
            {
                GetAnim();
            }
            else
            {
                GetText();
            }
            return this;
        }

        private void cbWordWrap_CheckedChanged(object sender, System.EventArgs e)
        {
            richTextBox1.WordWrap = cbWordWrap.Checked;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            GetText();
        }
    }
}
