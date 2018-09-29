using CASCLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    class FileScanner
    {
        private static readonly List<string> excludeFileTypes = new List<string>()
        {
            ".ogg", ".mp3", ".wav", ".avi", ".ttf", ".blp", ".sig", ".toc", ".blob", ".anim", ".skin", ".phys"
        };

        private static readonly List<string> extensions = new List<string>()
        {
            ".adt", ".anim", ".avi", ".blob", ".blp", ".bls", ".bone", ".db2", ".dbc", ".html", ".ini", ".lst", ".lua", ".m2", ".mp3", ".ogg",
            ".phys", ".sbt", ".sig", ".skin", ".tex", ".toc", ".ttf", ".txt", ".wdl", ".wdt", ".wfx", ".wmo", ".wtf", ".xml", ".xsd", ".zmp",
            "obj0.adt", "obj1.adt", "tex0.adt"
        };

        private static readonly Dictionary<byte[], string> LargeMagicNumbers = new Dictionary<byte[], string>()
        {
            { new byte[] { 0x52, 0x45, 0x56, 0x4D, 0x04, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x44, 0x48, 0x50 }, ".wdt" },
            { new byte[] { 0x52, 0x45, 0x56, 0x4D, 0x04, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x52, 0x44, 0x48 }, ".adt" },
            { new byte[] { 0x52, 0x45, 0x56, 0x4D, 0x04, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x58, 0x44, 0x4D }, "obj0.adt" },
            { new byte[] { 0x52, 0x45, 0x56, 0x4D, 0x04, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x44, 0x46, 0x4C }, "obj1.adt" },
            { new byte[] { 0x52, 0x45, 0x56, 0x4D, 0x04, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x50, 0x4D, 0x41 }, "tex0.adt" }

        };

        private static readonly Dictionary<byte[], string> MagicNumbers = new Dictionary<byte[], string>()
        {
            { new byte[] { 0x42, 0x4c, 0x50, 0x32 }, ".blp" },
            //{ new byte[] { 0x42, 0x4c, 0x50, 0x33 }, ".blp3" },
            { new byte[] { 0x4d, 0x44, 0x32, 0x30 }, ".m2" },
            { new byte[] { 0x4d, 0x44, 0x32, 0x31 }, ".m2" },
            { new byte[] { 0x53, 0x59, 0x48, 0x50 }, ".phys" },
            { new byte[] { 0x53, 0x4b, 0x49, 0x4e }, ".skin" },
            { new byte[] { 0x52, 0x45, 0x56, 0x4D }, ".wmo"  },
            { new byte[] { 0x00, 0x00, 0x00, 0x01 }, ".anim" },
            { new byte[] { 0x00, 0x00, 0x00, 0x00 }, ".anim" },
            { new byte[] { 0x3C, 0x3F, 0x78, 0x6D }, ".xml" },
            { new byte[] { 0x3C, 0x68, 0x74, 0x6D }, ".html" },
            { new byte[] { 0xEF, 0xBB, 0xBF, 0x3C, 0x68, 0x74, 0x6D }, ".html" },
            { new byte[] { 0x57, 0x44, 0x42, 0x43 }, ".dbc" },
            { new byte[] { 0x57, 0x44, 0x42, 0x35 }, ".db2" }, // WDB5
            { new byte[] { 0x57, 0x44, 0x42, 0x36 }, ".db2" }, // WDB6
            { new byte[] { 0x57, 0x44, 0x43, 0x31 }, ".db2" }, // WDC1
            { new byte[] { 0x52, 0x56, 0x58, 0x54 }, ".tex" },
            { new byte[] { 0x4f, 0x67, 0x67, 0x53 }, ".ogg" },
            { new byte[] { 0x48, 0x53, 0x58, 0x47 }, ".bls" },
            { new byte[] { 0x52, 0x49, 0x46, 0x46 }, ".wav" },
            { new byte[] { 0x44, 0x55, 0x54, 0x53 }, ".duts" },
            { new byte[] { 0x42, 0x4B, 0x48, 0x44 }, ".bkhd" },
            { new byte[] { 0x45, 0x45, 0x44, 0x43 }, ".eedc" },
            { new byte[] { 0x49, 0x44, 0x33 }, ".mp3" },
            { new byte[] { 0xff, 0xfb }, ".mp3" }
        };

        private CASCHandler CASC;
        private CASCFolder Root;

        public FileScanner(CASCHandler casc, CASCFolder root)
        {
            CASC = casc;
            Root = root;
        }

        public IEnumerable<string> ScanFile(CASCFile file)
        {
            if (excludeFileTypes.Contains(Path.GetExtension(file.FullName).ToLower()))
                yield break;

            Stream fileStream = null;

            try
            {
                fileStream = CASC.OpenFile(file.Hash);
            }
            catch (Exception exc)
            {
                Logger.WriteLine("Skipped {0}, error {1}.", file.FullName, exc.Message);
                yield break;
            }

            using (fileStream)
            {
                int b;
                int state = 1;
                StringBuilder sb = new StringBuilder();

                // look for regex a+(da+)*\.a+ where a = IsAlphaNum() and d = IsFileDelim()
                // using a simple state machine
                while ((b = fileStream.ReadByte()) > -1)
                {
                    if (state == 1 && IsAlphaNum(b) || state == 2 && IsAlphaNum(b) || state == 3 && IsAlphaNum(b)) // alpha
                    {
                        state = 2;
                        sb.Append((char)b);

                        if (sb.Length > 10)
                        {
                            int nextByte = fileStream.ReadByte();

                            if (nextByte == 0)
                            {
                                string foundStr = sb.ToString();

                                foreach (var ext in extensions)
                                    yield return foundStr + ext;
                            }

                            if (nextByte > -1)
                                fileStream.Position -= 1;
                        }
                    }
                    else if (state == 2 && IsFileDelim(b)) // delimiter
                    {
                        state = 3;
                        sb.Append((char)b);
                    }
                    else if (state == 2 && b == 46) // dot
                    {
                        state = 4;
                        sb.Append((char)b);
                    }
                    else if (state == 4 && IsLetterOrNumber(b)) // extension
                    {
                        state = 5;
                        sb.Append((char)b);
                    }
                    else if (state == 5 && IsLetterOrNumber(b)) // extension
                    {
                        sb.Append((char)b);
                    }
                    else if (state == 5 && !IsFileChar(b)) // accept
                    {
                        state = 1;
                        if (sb.Length >= 10)
                            yield return sb.ToString();
                        sb.Clear();
                    }
                    else
                    {
                        state = 1;
                        sb.Clear();
                    }
                }
            }
        }

        // space, '(', ')', dash, dot, slash, backslash, underscore, 0-9, A-Z, a-z
        private bool IsFileChar(int i)
        {
            return i == 46 || IsFileDelim(i) || IsSpecialChar(i) || IsLetterOrNumber(i);
        }

        // space, '(', ')', dash, underscore, 0-9, A-Z, a-z
        private bool IsAlphaNum(int i)
        {
            return IsSpecialChar(i) || IsLetterOrNumber(i);
        }

        // slash or backslash
        private bool IsFileDelim(int i)
        {
            return i == 47 || i == 92;
        }

        private bool IsSpecialChar(int i)
        {
            return i == 32 || i == 33 || i == 37 || i == 38 || i == 40 || i == 41 || i == 43 || i == 45 || i == 91 || i == 93 || i == 95;
        }

        // 0-9 || A-Z || a-z
        private bool IsLetterOrNumber(int i)
        {
            return (i >= 48 && i <= 57) || (i >= 65 && i <= 90) || (i >= 97 && i <= 122);
        }

        public string GetFileExtension(CASCFile file)
        {
            try
            {
                using (Stream stream = CASC.OpenFile(file.Hash))
                {
                    byte[] magic = new byte[15];
                    stream.Read(magic, 0, magic.Length);

                    foreach (var number in MagicNumbers)
                    {
                        if (number.Key.EqualsToIgnoreLength(magic))
                        {
                            return number.Value;
                        }
                    }

                    foreach (var number in LargeMagicNumbers)
                    {
                        if (number.Key.EqualsToIgnoreLength(magic))
                        {
                            return number.Value;
                        }
                    }
                }
            }
            catch
            { }
            return string.Empty;
        }
    }
}
