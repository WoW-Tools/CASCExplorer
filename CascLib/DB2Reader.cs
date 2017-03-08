using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    public class DB2Row
    {
        private readonly byte[] m_data;
        private readonly DB2Reader m_reader;

        public byte[] Data => m_data;

        public DB2Row(DB2Reader reader, byte[] data)
        {
            m_reader = reader;
            m_data = data;
        }

        public T GetField<T>(int field)
        {
            object retVal;

            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.String:
                    int start = BitConverter.ToInt32(m_data, field * 4), len = 0;
                    while (m_reader.StringTable[start + len] != 0)
                        len++;
                    retVal = Encoding.UTF8.GetString(m_reader.StringTable, start, len);
                    return (T)retVal;
                case TypeCode.Int32:
                    retVal = BitConverter.ToInt32(m_data, field * 4);
                    return (T)retVal;
                case TypeCode.Single:
                    retVal = BitConverter.ToSingle(m_data, field * 4);
                    return (T)retVal;
                default:
                    return default(T);
            }
        }
    }

    public class DB2Reader : IEnumerable<KeyValuePair<int, DB2Row>>
    {
        private const int HeaderSize = 48;
        private const uint DB2FmtSig = 0x32424457;          // WDB2

        public int RecordsCount { get; private set; }
        public int FieldsCount { get; private set; }
        public int RecordSize { get; private set; }
        public int StringTableSize { get; private set; }
        public int MinIndex { get; private set; }
        public int MaxIndex { get; private set; }

        private readonly DB2Row[] m_rows;
        public byte[] StringTable { get; private set; }

        readonly Dictionary<int, DB2Row> m_index = new Dictionary<int, DB2Row>();

        public DB2Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public DB2Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                {
                    throw new InvalidDataException(string.Format("DB2 file is corrupted!"));
                }

                if (reader.ReadUInt32() != DB2FmtSig)
                {
                    throw new InvalidDataException(string.Format("DB2 file is corrupted!"));
                }

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                // WDB2 specific fields
                uint tableHash = reader.ReadUInt32();   // new field in WDB2
                uint build = reader.ReadUInt32();       // new field in WDB2
                uint unk1 = reader.ReadUInt32();        // new field in WDB2

                if (build > 12880) // new extended header
                {
                    int MinId = reader.ReadInt32();     // new field in WDB2
                    int MaxId = reader.ReadInt32();     // new field in WDB2
                    int locale = reader.ReadInt32();    // new field in WDB2
                    int unk5 = reader.ReadInt32();      // new field in WDB2

                    if (MaxId != 0)
                    {
                        var diff = MaxId - MinId + 1;   // blizzard is weird people...
                        reader.ReadBytes(diff * 4);     // an index for rows
                        reader.ReadBytes(diff * 2);     // a memory allocation bank
                    }
                }

                m_rows = new DB2Row[RecordsCount];

                for (int i = 0; i < RecordsCount; i++)
                {
                    m_rows[i] = new DB2Row(this, reader.ReadBytes(RecordSize));

                    int idx = BitConverter.ToInt32(m_rows[i].Data, 0);

                    if (idx < MinIndex)
                        MinIndex = idx;

                    if (idx > MaxIndex)
                        MaxIndex = idx;

                    m_index[idx] = m_rows[i];
                }

                StringTable = reader.ReadBytes(StringTableSize);
            }
        }

        public bool HasRow(int index)
        {
            return m_index.ContainsKey(index);
        }

        public DB2Row GetRow(int index)
        {
            m_index.TryGetValue(index, out DB2Row row);
            return row;
        }

        public IEnumerator<KeyValuePair<int, DB2Row>> GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_index.GetEnumerator();
        }
    }
}
