﻿using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CASCExplorer.ViewPlugin;
using System.IO;
using System.ComponentModel.Composition;

namespace CASCExplorer.DefaultViews.Previews
{
    [Export(typeof(IPreview))]
    [ExportMetadata("Extensions", new string[] { ".txt", ".ini", ".wtf", ".lua", ".toc", ".xml", ".htm", ".html", ".lst", ".signed" })]
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

        public Control Show(Stream stream, string fileName)
        {
            m_bytes = new byte[stream.Length];
            stream.Read(m_bytes, 0, (int)stream.Length);
            GetText();
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