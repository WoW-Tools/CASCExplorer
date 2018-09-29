using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CASCLib
{
    public interface IDB2Row
    {
        int Id { get; set; }
        T GetField<T>(int fieldIndex, int arrayIndex = -1);
        IDB2Row Clone();
    }

    public abstract class DB2Reader : IEnumerable<KeyValuePair<int, IDB2Row>>
    {
        public int RecordsCount { get; protected set; }
        public int FieldsCount { get; protected set; }
        public int RecordSize { get; protected set; }
        public int StringTableSize { get; protected set; }
        public uint TableHash { get; protected set; }
        public uint LayoutHash { get; protected set; }
        public int MinIndex { get; protected set; }
        public int MaxIndex { get; protected set; }
        public int IdFieldIndex { get; protected set; }

        protected FieldMetaData[] m_meta;
        public FieldMetaData[] Meta => m_meta;

        protected int[] m_indexData;
        public int[] IndexData => m_indexData;

        protected ColumnMetaData[] m_columnMeta;
        public ColumnMetaData[] ColumnMeta => m_columnMeta;

        protected Value32[][] m_palletData;
        public Value32[][] PalletData => m_palletData;

        protected Dictionary<int, Value32>[] m_commonData;
        public Dictionary<int, Value32>[] CommonData => m_commonData;

        public Dictionary<long, string> StringTable => m_stringsTable;

        protected Dictionary<int, IDB2Row> _Records = new Dictionary<int, IDB2Row>();

        // normal records data
        protected byte[] recordsData;
        protected Dictionary<long, string> m_stringsTable;

        // sparse records data
        protected byte[] sparseData;
        protected SparseEntry[] sparseEntries;

        public bool HasRow(int id)
        {
            return _Records.ContainsKey(id);
        }

        public IDB2Row GetRow(int id)
        {
            _Records.TryGetValue(id, out IDB2Row row);
            return row;
        }

        public IEnumerator<KeyValuePair<int, IDB2Row>> GetEnumerator()
        {
            return _Records.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Records.GetEnumerator();
        }
    }

    public struct FieldMetaData
    {
        public short Bits;
        public short Offset;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ColumnMetaData
    {
        [FieldOffset(0)]
        public ushort RecordOffset;
        [FieldOffset(2)]
        public ushort Size;
        [FieldOffset(4)]
        public uint AdditionalDataSize;
        [FieldOffset(8)]
        public CompressionType CompressionType;
        [FieldOffset(12)]
        public ColumnCompressionData_Immediate Immediate;
        [FieldOffset(12)]
        public ColumnCompressionData_Pallet Pallet;
        [FieldOffset(12)]
        public ColumnCompressionData_Common Common;
    }

    public struct ColumnCompressionData_Immediate
    {
        public int BitOffset;
        public int BitWidth;
        public int Flags; // 0x1 signed
    }

    public struct ColumnCompressionData_Pallet
    {
        public int BitOffset;
        public int BitWidth;
        public int Cardinality;
    }

    public struct ColumnCompressionData_Common
    {
        public Value32 DefaultValue;
        public int B;
        public int C;
    }

    public struct Value32
    {
        private uint Value;

        public T As<T>() where T : unmanaged
        {
            return Unsafe.As<uint, T>(ref Value);
        }
    }

    public enum CompressionType
    {
        None = 0,
        Immediate = 1,
        Common = 2,
        Pallet = 3,
        PalletArray = 4,
        SignedImmediate = 5
    }

    public struct ReferenceEntry
    {
        public int Id;
        public int Index;
    }

    public class ReferenceData
    {
        public int NumRecords { get; set; }
        public int MinId { get; set; }
        public int MaxId { get; set; }
        public ReferenceEntry[] Entries { get; set; }
    }

    [Flags]
    public enum DB2Flags
    {
        None = 0x0,
        Sparse = 0x1,
        SecondaryKey = 0x2,
        Index = 0x4,
        Unknown1 = 0x8,
        Unknown2 = 0x10
    }

    public struct SparseEntry
    {
        public uint Offset;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SectionHeader
    {
        public ulong TactKeyLookup;
        public int FileOffset;
        public int NumRecords;
        public int StringTableSize;
        public int CopyTableSize;
        public int SparseTableOffset; // CatalogDataOffset, absolute value, {uint offset, ushort size}[MaxId - MinId + 1]
        public int IndexDataSize; // int indexData[IndexDataSize / 4]
        public int ParentLookupDataSize; // uint NumRecords, uint minId, uint maxId, {uint id, uint index}[NumRecords], questionable usefulness...
    }

    public class BitReader
    {
        private byte[] m_array;
        private int m_readPos;
        private int m_readOffset;

        public int Position { get => m_readPos; set => m_readPos = value; }
        public int Offset { get => m_readOffset; set => m_readOffset = value; }
        public byte[] Data { get => m_array; set => m_array = value; }

        public BitReader(byte[] data)
        {
            m_array = data;
        }

        public BitReader(byte[] data, int offset)
        {
            m_array = data;
            m_readOffset = offset;
        }

        public uint ReadUInt32(int numBits)
        {
            uint result = Unsafe.As<byte, uint>(ref m_array[m_readOffset + (m_readPos >> 3)]) << (32 - numBits - (m_readPos & 7)) >> (32 - numBits);
            m_readPos += numBits;
            return result;
        }

        public T Read<T>(int numBits) where T : unmanaged
        {
            ulong result = Unsafe.As<byte, ulong>(ref m_array[m_readOffset + (m_readPos >> 3)]) << (64 - numBits - (m_readPos & 7)) >> (64 - numBits);
            return Unsafe.As<ulong, T>(ref result);
        }

        public T ReadSigned<T>(int numBits) where T : unmanaged
        {
            ulong result = Unsafe.As<byte, ulong>(ref m_array[m_readOffset + (m_readPos >> 3)]) << (64 - numBits - (m_readPos & 7)) >> (64 - numBits);
            ulong signedShift = (1UL << (numBits - 1));
            result = (signedShift ^ result) - signedShift;
            return Unsafe.As<ulong, T>(ref result);
        }
    }
}
