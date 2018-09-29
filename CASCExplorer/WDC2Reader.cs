using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCLib
{
    public class WDC2Row : IDB2Row
    {
        private BitReader m_data;
        private DB2Reader m_reader;
        private int m_dataOffset;
        private int m_recordsOffset;

        public int Id { get; set; }

        private FieldMetaData[] m_fieldMeta;
        private ColumnMetaData[] m_columnMeta;
        private Value32[][] m_palletData;
        private Dictionary<int, Value32>[] m_commonData;
        private ReferenceEntry? m_refData;

        public WDC2Row(DB2Reader reader, BitReader data, int recordsOffset, int id, ReferenceEntry? refData)
        {
            m_reader = reader;
            m_data = data;
            m_recordsOffset = recordsOffset;

            m_dataOffset = m_data.Offset;

            m_fieldMeta = reader.Meta;
            m_columnMeta = reader.ColumnMeta;
            m_palletData = reader.PalletData;
            m_commonData = reader.CommonData;
            m_refData = refData;

            if (id != -1)
                Id = id;
            else
            {
                int idFieldIndex = reader.IdFieldIndex;

                m_data.Position = m_columnMeta[idFieldIndex].RecordOffset;

                Id = GetFieldValue<int>(0, m_data, m_fieldMeta[idFieldIndex], m_columnMeta[idFieldIndex], m_palletData[idFieldIndex], m_commonData[idFieldIndex]);
            }
        }

        private static Dictionary<Type, Func<int, BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, object>> simpleReaders = new Dictionary<Type, Func<int, BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, object>>
        {
            [typeof(float)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<float>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(int)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(uint)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<uint>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(short)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<short>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(ushort)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<ushort>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(sbyte)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<sbyte>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(byte)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValue<byte>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(string)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => { var pos = recordsOffset + data.Offset + (data.Position >> 3); int strOfs = GetFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData); return stringTable[pos + strOfs]; },
        };

        private static Dictionary<Type, Func<BitReader, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<BitReader, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, int, object>>
        {
            [typeof(float)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => GetFieldValueArray<float>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(int)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => GetFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(uint)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => GetFieldValueArray<uint>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(ulong)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => GetFieldValueArray<ulong>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(ushort)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => GetFieldValueArray<ushort>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(byte)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => GetFieldValueArray<byte>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex],
            [typeof(string)] = (data, fieldMeta, columnMeta, palletData, commonData, stringTable, arrayIndex) => { int strOfs = GetFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData, arrayIndex + 1)[arrayIndex]; return stringTable[strOfs]; },
        };

        public T GetField<T>(int fieldIndex, int arrayIndex = -1)
        {
            object value = null;

            if (fieldIndex >= m_reader.Meta.Length)
            {
                value = m_refData?.Id ?? 0;
                return (T)value;
            }

            m_data.Position = m_columnMeta[fieldIndex].RecordOffset;
            m_data.Offset = m_dataOffset;

            if (arrayIndex >= 0)
            {
                if (arrayReaders.TryGetValue(typeof(T), out var reader))
                    value = reader(m_data, m_fieldMeta[fieldIndex], m_columnMeta[fieldIndex], m_palletData[fieldIndex], m_commonData[fieldIndex], m_reader.StringTable, arrayIndex);
                else
                    throw new Exception("Unhandled array type: " + typeof(T).Name);
            }
            else
            {
                if (simpleReaders.TryGetValue(typeof(T), out var reader))
                    value = reader(Id, m_data, m_recordsOffset, m_fieldMeta[fieldIndex], m_columnMeta[fieldIndex], m_palletData[fieldIndex], m_commonData[fieldIndex], m_reader.StringTable);
                else
                    throw new Exception("Unhandled field type: " + typeof(T).Name);
            }

            return (T)value;
        }

        private static T GetFieldValue<T>(int Id, BitReader r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, Value32[] palletData, Dictionary<int, Value32> commonData) where T : unmanaged
        {
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    int bitSize = 32 - fieldMeta.Bits;
                    if (bitSize > 0)
                        return r.Read<T>(bitSize);
                    else
                        return r.Read<T>(columnMeta.Immediate.BitWidth);
                case CompressionType.Immediate:
                    return r.Read<T>(columnMeta.Immediate.BitWidth);
                case CompressionType.SignedImmediate:
                    return r.ReadSigned<T>(columnMeta.Immediate.BitWidth);
                case CompressionType.Common:
                    if (commonData.TryGetValue(Id, out Value32 val))
                        return val.As<T>();
                    else
                        return columnMeta.Common.DefaultValue.As<T>();
                case CompressionType.Pallet:
                    uint palletIndex = r.ReadUInt32(columnMeta.Pallet.BitWidth);

                    T val1 = palletData[palletIndex].As<T>();

                    return val1;
            }
            throw new Exception(string.Format("Unexpected compression type {0}", columnMeta.CompressionType));
        }

        private static T[] GetFieldValueArray<T>(BitReader r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, Value32[] palletData, Dictionary<int, Value32> commonData, int arraySize) where T : unmanaged
        {
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    int bitSize = 32 - fieldMeta.Bits;

                    T[] arr1 = new T[arraySize];

                    for (int i = 0; i < arr1.Length; i++)
                    {
                        if (bitSize > 0)
                            arr1[i] = r.Read<T>(bitSize);
                        else
                            arr1[i] = r.Read<T>(columnMeta.Immediate.BitWidth);
                    }

                    return arr1;
                case CompressionType.Immediate:
                    T[] arr2 = new T[arraySize];

                    for (int i = 0; i < arr2.Length; i++)
                        arr2[i] = r.Read<T>(columnMeta.Immediate.BitWidth);

                    return arr2;
                case CompressionType.SignedImmediate:
                    T[] arr4 = new T[arraySize];

                    for (int i = 0; i < arr4.Length; i++)
                        arr4[i] = r.ReadSigned<T>(columnMeta.Immediate.BitWidth);

                    return arr4;
                case CompressionType.PalletArray:
                    int cardinality = columnMeta.Pallet.Cardinality;

                    if (arraySize != cardinality)
                        throw new Exception("Struct missmatch for pallet array field?");

                    uint palletArrayIndex = r.ReadUInt32(columnMeta.Pallet.BitWidth);

                    T[] arr3 = new T[cardinality];

                    for (int i = 0; i < arr3.Length; i++)
                        arr3[i] = palletData[i + cardinality * (int)palletArrayIndex].As<T>();

                    return arr3;
            }
            throw new Exception(string.Format("Unexpected compression type {0}", columnMeta.CompressionType));
        }

        public IDB2Row Clone()
        {
            return (IDB2Row)MemberwiseClone();
        }
    }

    public class WDC2Reader : DB2Reader
    {
        private const int HeaderSize = 72 + 1 * 36;
        private const uint WDC2FmtSig = 0x32434457; // WDC2

        public WDC2Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDC2Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException(String.Format("WDC2 file is corrupted!"));

                uint magic = reader.ReadUInt32();

                if (magic != WDC2FmtSig)
                    throw new InvalidDataException(String.Format("WDC2 file is corrupted!"));

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                LayoutHash = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                int locale = reader.ReadInt32();
                int flags = reader.ReadUInt16();
                IdFieldIndex = reader.ReadUInt16();
                int totalFieldsCount = reader.ReadInt32();
                int packedDataOffset = reader.ReadInt32(); // Offset within the field where packed data starts
                int lookupColumnCount = reader.ReadInt32(); // count of lookup columns
                int columnMetaDataSize = reader.ReadInt32(); // 24 * NumFields bytes, describes column bit packing, {ushort recordOffset, ushort size, uint additionalDataSize, uint compressionType, uint packedDataOffset or commonvalue, uint cellSize, uint cardinality}[NumFields], sizeof(DBC2CommonValue) == 8
                int commonDataSize = reader.ReadInt32();
                int palletDataSize = reader.ReadInt32(); // in bytes, sizeof(DBC2PalletValue) == 4
                int sectionsCount = reader.ReadInt32();

                if (sectionsCount > 1)
                    throw new Exception("sectionsCount > 1");

                SectionHeader[] sections = reader.ReadArray<SectionHeader>(sectionsCount);

                // field meta data
                m_meta = reader.ReadArray<FieldMetaData>(FieldsCount);

                // column meta data
                m_columnMeta = reader.ReadArray<ColumnMetaData>(FieldsCount);

                // pallet data
                m_palletData = new Value32[m_columnMeta.Length][];

                for (int i = 0; i < m_columnMeta.Length; i++)
                {
                    if (m_columnMeta[i].CompressionType == CompressionType.Pallet || m_columnMeta[i].CompressionType == CompressionType.PalletArray)
                    {
                        m_palletData[i] = reader.ReadArray<Value32>((int)m_columnMeta[i].AdditionalDataSize / 4);
                    }
                }

                // common data
                m_commonData = new Dictionary<int, Value32>[m_columnMeta.Length];

                for (int i = 0; i < m_columnMeta.Length; i++)
                {
                    if (m_columnMeta[i].CompressionType == CompressionType.Common)
                    {
                        Dictionary<int, Value32> commonValues = new Dictionary<int, Value32>();
                        m_commonData[i] = commonValues;

                        for (int j = 0; j < m_columnMeta[i].AdditionalDataSize / 8; j++)
                            commonValues[reader.ReadInt32()] = reader.Read<Value32>();
                    }
                }

                for (int sectionIndex = 0; sectionIndex < sectionsCount; sectionIndex++)
                {
                    reader.BaseStream.Position = sections[sectionIndex].FileOffset;

                    if ((flags & 0x1) == 0)
                    {
                        // records data
                        recordsData = reader.ReadBytes(sections[sectionIndex].NumRecords * RecordSize);

                        Array.Resize(ref recordsData, recordsData.Length + 8); // pad with extra zeros so we don't crash when reading

                        // string data
                        m_stringsTable = new Dictionary<long, string>();

                        for (int i = 0; i < sections[sectionIndex].StringTableSize;)
                        {
                            long oldPos = reader.BaseStream.Position;

                            m_stringsTable[oldPos] = reader.ReadCString();

                            i += (int)(reader.BaseStream.Position - oldPos);
                        }
                    }
                    else
                    {
                        // sparse data with inlined strings
                        sparseData = reader.ReadBytes(sections[sectionIndex].SparseTableOffset - sections[sectionIndex].FileOffset);

                        if (reader.BaseStream.Position != sections[sectionIndex].SparseTableOffset)
                            throw new Exception("reader.BaseStream.Position != sections[sectionIndex].SparseTableOffset");

                        sparseEntries = reader.ReadArray<SparseEntry>(MaxIndex - MinIndex + 1);

                        if (sections[sectionIndex].SparseTableOffset != 0)
                            throw new Exception("Sparse Table NYI!");
                        else
                            throw new Exception("Sparse Table with zero offset?");
                    }

                    // index data
                    m_indexData = reader.ReadArray<int>(sections[sectionIndex].IndexDataSize / 4);

                    // duplicate rows data
                    Dictionary<int, int> copyData = new Dictionary<int, int>();

                    for (int i = 0; i < sections[sectionIndex].CopyTableSize / 8; i++)
                        copyData[reader.ReadInt32()] = reader.ReadInt32();

                    // reference data
                    ReferenceData refData = null;

                    if (sections[sectionIndex].ParentLookupDataSize > 0)
                    {
                        refData = new ReferenceData
                        {
                            NumRecords = reader.ReadInt32(),
                            MinId = reader.ReadInt32(),
                            MaxId = reader.ReadInt32()
                        };

                        refData.Entries = reader.ReadArray<ReferenceEntry>(refData.NumRecords);
                    }

                    BitReader bitReader = new BitReader(recordsData);

                    for (int i = 0; i < RecordsCount; ++i)
                    {
                        bitReader.Position = 0;
                        bitReader.Offset = i * RecordSize;

                        IDB2Row rec = new WDC2Row(this, bitReader, sections[sectionIndex].FileOffset, sections[sectionIndex].IndexDataSize != 0 ? m_indexData[i] : -1, refData?.Entries[i]);

                        if (sections[sectionIndex].IndexDataSize != 0)
                            _Records.Add(m_indexData[i], rec);
                        else
                            _Records.Add(rec.Id, rec);

                        if (i % 1000 == 0)
                            Console.Write("\r{0} records read", i);
                    }

                    foreach (var copyRow in copyData)
                    {
                        IDB2Row rec = _Records[copyRow.Value].Clone();
                        rec.Id = copyRow.Key;
                        _Records.Add(copyRow.Key, rec);
                    }
                }
            }
        }
    }
}
