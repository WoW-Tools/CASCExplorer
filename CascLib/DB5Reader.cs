using System; 
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CASCExplorer
{
    public class DB5Row
    {
        private byte[] m_data;
        private DB5Reader m_reader;

        public byte[] Data => m_data;

        public DB5Row(DB5Reader reader, byte[] data)
        {
            m_reader = reader;
            m_data = data;
        }

        public T GetField<T>(int field, int arrayIndex = 0)
        {
            DB2Meta meta = m_reader.Meta[field];

            if (meta.Bits != 0x00 && meta.Bits != 0x08 && meta.Bits != 0x10 && meta.Bits != 0x18 && meta.Bits != -32)
                throw new Exception("Unknown meta.Flags");

            int bytesCount = (32 - meta.Bits) >> 3;

            TypeCode code = Type.GetTypeCode(typeof(T));

            object value = null;

            switch (code)
            {
                case TypeCode.Byte:
                    if (meta.Bits != 0x18)
                        throw new Exception("TypeCode.Byte Unknown meta.Bits");
                    value = m_data[meta.Offset + bytesCount * arrayIndex];
                    break;
                case TypeCode.SByte:
                    if (meta.Bits != 0x18)
                        throw new Exception("TypeCode.SByte Unknown meta.Bits");
                    value = (sbyte)m_data[meta.Offset + bytesCount * arrayIndex];
                    break;
                case TypeCode.Int16:
                    if (meta.Bits != 0x10)
                        throw new Exception("TypeCode.Int16 Unknown meta.Bits");
                    value = BitConverter.ToInt16(m_data, meta.Offset + bytesCount * arrayIndex);
                    break;
                case TypeCode.UInt16:
                    if (meta.Bits != 0x10)
                        throw new Exception("TypeCode.UInt16 Unknown meta.Bits");
                    value = BitConverter.ToUInt16(m_data, meta.Offset + bytesCount * arrayIndex);
                    break;
                case TypeCode.Int32:
                    byte[] b1 = new byte[4];
                    Array.Copy(m_data, meta.Offset + bytesCount * arrayIndex, b1, 0, bytesCount);
                    value = BitConverter.ToInt32(b1, 0);
                    break;
                case TypeCode.UInt32:
                    byte[] b2 = new byte[4];
                    Array.Copy(m_data, meta.Offset + bytesCount * arrayIndex, b2, 0, bytesCount);
                    value = BitConverter.ToUInt32(b2, 0);
                    break;
                case TypeCode.Int64:
                    byte[] b3 = new byte[8];
                    Array.Copy(m_data, meta.Offset + bytesCount * arrayIndex, b3, 0, bytesCount);
                    value = BitConverter.ToInt64(b3, 0);
                    break;
                case TypeCode.UInt64:
                    byte[] b4 = new byte[8];
                    Array.Copy(m_data, meta.Offset + bytesCount * arrayIndex, b4, 0, bytesCount);
                    value = BitConverter.ToUInt64(b4, 0);
                    break;
                case TypeCode.String:
                    if (meta.Bits != 0x00)
                        throw new Exception("TypeCode.String Unknown meta.Bits");
                    byte[] b5 = new byte[4];
                    Array.Copy(m_data, meta.Offset + bytesCount * arrayIndex, b5, 0, bytesCount);
                    int start = BitConverter.ToInt32(b5, 0), len = 0;
                    while (m_reader.StringTable[start + len] != 0)
                        len++;
                    value = Encoding.UTF8.GetString(m_reader.StringTable, start, len);
                    break;
                case TypeCode.Single:
                    if (meta.Bits != 0x00)
                        throw new Exception("TypeCode.Single Unknown meta.Bits");
                    value = BitConverter.ToSingle(m_data, meta.Offset + bytesCount * arrayIndex);
                    break;
                default:
                    throw new Exception("Unknown TypeCode " + code);
            }

            return (T)value;
        }
    }

    public class DB2Meta
    {
        public short Bits { get; set; }
        public short Offset { get; set; }
    }

    public class DB5Reader : IEnumerable<KeyValuePair<int, DB5Row>>
    {
        private const int HeaderSize = 48;
        private const uint DB5FmtSig = 0x35424457;          // WDB5

        public int RecordsCount { get; private set; }
        public int FieldsCount { get; private set; }
        public int RecordSize { get; private set; }
        public int StringTableSize { get; private set; }
        public int MinIndex { get; private set; }
        public int MaxIndex { get; private set; }

        private readonly byte[] m_stringTable;
        private readonly DB2Meta[] m_meta;

        public byte[] StringTable => m_stringTable;
        public DB2Meta[] Meta => m_meta;

        private Dictionary<int, DB5Row> m_index = new Dictionary<int, DB5Row>();

        public DB5Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public DB5Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                {
                    throw new InvalidDataException(String.Format("DB5 file is corrupted!"));
                }

                uint magic = reader.ReadUInt32();

                if (magic != DB5FmtSig)
                {
                    throw new InvalidDataException(String.Format("DB5 file is corrupted!"));
                }

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                uint tableHash = reader.ReadUInt32();
                uint layoutHash = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                int locale = reader.ReadInt32();
                int copyTableSize = reader.ReadInt32();
                int flags = reader.ReadUInt16();
                int idIndex = reader.ReadUInt16();

                bool isSparse = (flags & 0x1) != 0;
                bool hasIndex = (flags & 0x4) != 0;

                m_meta = new DB2Meta[FieldsCount];

                for (int i = 0; i < m_meta.Length; i++)
                {
                    m_meta[i] = new DB2Meta()
                    {
                        Bits = reader.ReadInt16(),
                        Offset = reader.ReadInt16()
                    };
                }

                DB5Row[] m_rows = new DB5Row[RecordsCount];

                for (int i = 0; i < RecordsCount; i++)
                {
                    m_rows[i] = new DB5Row(this, reader.ReadBytes(RecordSize));
                }

                m_stringTable = reader.ReadBytes(StringTableSize);

                if (isSparse)
                {
                    // code...
                    throw new Exception("can't do sparse table");
                }

                if (hasIndex)
                {
                    for (int i = 0; i < RecordsCount; i++)
                    {
                        int id = reader.ReadInt32();
                        m_index[id] = m_rows[i];
                    }
                }
                else
                {
                    for (int i = 0; i < RecordsCount; i++)
                    {
                        int id = m_rows[i].Data.Skip(m_meta[idIndex].Offset).Take((32 - m_meta[idIndex].Bits) >> 3).Select((b, k) => b << k * 8).Sum();
                        m_index[id] = m_rows[i];
                    }
                }

                if (copyTableSize > 0)
                {
                    int copyCount = copyTableSize / 8;

                    for (int i = 0; i < copyCount; i++)
                    {
                        int newId = reader.ReadInt32();
                        int oldId = reader.ReadInt32();

                        m_index[newId] = m_index[oldId];
                    }
                }
            }
        }

        public bool HasRow(int id)
        {
            return m_index.ContainsKey(id);
        }

        public DB5Row GetRow(int id)
        {
            if (!m_index.ContainsKey(id))
                return null;

            return m_index[id];
        }

        public IEnumerator<KeyValuePair<int, DB5Row>> GetEnumerator()
        {
            return m_index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_index.GetEnumerator();
        }
    }
}
