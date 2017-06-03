using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    public class DB3Row
    {
        private byte[] m_data;
        private DB3Reader m_reader;

        public byte[] Data { get { return m_data; } }

        public DB3Row(DB3Reader reader, byte[] data)
        {
            m_reader = reader;
            m_data = data;
        }

        public unsafe T GetField<T>(int offset)
        {
            object retVal;

            fixed(byte *ptr = m_data)
            {
                switch (Type.GetTypeCode(typeof(T)))
                {
                    case TypeCode.String:
                        string str;
                        int start = BitConverter.ToInt32(m_data, offset);
                        if (m_reader.StringTable.TryGetValue(start, out str))
                            retVal = str;
                        else
                            retVal = string.Empty;
                        return (T)retVal;
                    case TypeCode.SByte:
                        retVal = ptr[offset];
                        return (T)retVal;
                    case TypeCode.Byte:
                        retVal = ptr[offset];
                        return (T)retVal;
                    case TypeCode.Int16:
                        retVal = *(short*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.UInt16:
                        retVal = *(ushort*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.Int32:
                        retVal = *(int*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.UInt32:
                        retVal = *(uint*)(ptr + offset);
                        return (T)retVal;
                    case TypeCode.Single:
                        retVal = *(float*)(ptr + offset);
                        return (T)retVal;
                    default:
                        return default(T);
                }
            }
        }
    }

    public class DB3Reader : IEnumerable<KeyValuePair<int, DB3Row>>
    {
        private readonly int HeaderSize;
        private const uint DB3FmtSig = 0x33424457;          // WDB3
        private const uint DB4FmtSig = 0x34424457;          // WDB4

        public int RecordsCount { get; private set; }
        public int FieldsCount { get; private set; }
        public int RecordSize { get; private set; }
        public int StringTableSize { get; private set; }
        public int MinIndex { get; private set; }
        public int MaxIndex { get; private set; }

        public Dictionary<int, string> StringTable { get; private set; }

        private SortedDictionary<int, DB3Row> m_index = new SortedDictionary<int, DB3Row>();

        public DB3Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public DB3Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                {
                    throw new InvalidDataException(string.Format("DB3 file is corrupted!"));
                }

                uint magic = reader.ReadUInt32();

                if (magic != DB3FmtSig && magic != DB4FmtSig)
                {
                    throw new InvalidDataException(string.Format("DB3 file is corrupted!"));
                }

                if (magic == DB3FmtSig)
                    HeaderSize = 48;
                else if (magic == DB4FmtSig)
                    HeaderSize = 52;
                else
                    HeaderSize = 56;

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                uint tableHash = reader.ReadUInt32();
                uint build = reader.ReadUInt32();

                uint unk1 = reader.ReadUInt32(); // timemodified

                int MinId = reader.ReadInt32();
                int MaxId = reader.ReadInt32();
                int locale = reader.ReadInt32();
                int CopyTableSize = reader.ReadInt32();

                if (magic == DB4FmtSig)
                {
                    int metaFlags = reader.ReadInt32();
                }

                int stringTableStart = HeaderSize + RecordsCount * RecordSize;
                int stringTableEnd = stringTableStart + StringTableSize;

                // Index table
                int[] m_indexes = null;
                bool hasIndex = stringTableEnd + CopyTableSize < reader.BaseStream.Length;

                if (hasIndex)
                {
                    reader.BaseStream.Position = stringTableEnd;

                    m_indexes = new int[RecordsCount];

                    for (int i = 0; i < RecordsCount; i++)
                        m_indexes[i] = reader.ReadInt32();
                }

                // Records table
                reader.BaseStream.Position = HeaderSize;

                for (int i = 0; i < RecordsCount; i++)
                {
                    byte[] recordBytes = reader.ReadBytes(RecordSize);

                    if (hasIndex)
                    {
                        byte[] newRecordBytes = new byte[RecordSize + 4];

                        Array.Copy(BitConverter.GetBytes(m_indexes[i]), newRecordBytes, 4);
                        Array.Copy(recordBytes, 0, newRecordBytes, 4, recordBytes.Length);

                        m_index.Add(m_indexes[i], new DB3Row(this, newRecordBytes));
                    }
                    else
                    {
                        m_index.Add(BitConverter.ToInt32(recordBytes, 0), new DB3Row(this, recordBytes));
                    }
                }

                // Strings table
                reader.BaseStream.Position = stringTableStart;

                StringTable = new Dictionary<int, string>();

                while (reader.BaseStream.Position != stringTableEnd)
                {
                    int index = (int)reader.BaseStream.Position - stringTableStart;
                    StringTable[index] = reader.ReadCString();
                }

                // Copy index table
                long copyTablePos = stringTableEnd + (hasIndex ? 4 * RecordsCount : 0);

                if (copyTablePos != reader.BaseStream.Length && CopyTableSize != 0)
                {
                    reader.BaseStream.Position = copyTablePos;

                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        int id = reader.ReadInt32();
                        int idcopy = reader.ReadInt32();

                        RecordsCount++;

                        DB3Row copyRow = m_index[idcopy];
                        byte[] newRowData = new byte[copyRow.Data.Length];
                        Array.Copy(copyRow.Data, newRowData, newRowData.Length);
                        Array.Copy(BitConverter.GetBytes(id), newRowData, 4);

                        m_index.Add(id, new DB3Row(this, newRowData));
                    }
                }
            }
        }

        public bool HasRow(int index)
        {
            return m_index.ContainsKey(index);
        }

        public DB3Row GetRow(int index)
        {
            m_index.TryGetValue(index, out DB3Row row);
            return row;
        }

        public IEnumerator<KeyValuePair<int, DB3Row>> GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_index.GetEnumerator();
        }
    }
}
