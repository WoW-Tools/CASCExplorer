using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CASCLib
{
    public static class FastStruct<T> where T : struct
    {
        private delegate T LoadFromByteRefDelegate(ref byte source);
        private delegate void CopyMemoryDelegate(ref T dest, ref byte src, int count);

        private readonly static LoadFromByteRefDelegate LoadFromByteRef = BuildLoadFromByteRefMethod();
        private readonly static CopyMemoryDelegate CopyMemory = BuildCopyMemoryMethod();

        public static readonly int Size = Marshal.SizeOf<T>();

        private static LoadFromByteRefDelegate BuildLoadFromByteRefMethod()
        {
            var methodLoadFromByteRef = new DynamicMethod("LoadFromByteRef<" + typeof(T).FullName + ">",
                typeof(T), new[] { typeof(byte).MakeByRefType() }, typeof(FastStruct<T>));

            ILGenerator generator = methodLoadFromByteRef.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (LoadFromByteRefDelegate)methodLoadFromByteRef.CreateDelegate(typeof(LoadFromByteRefDelegate));
        }

        private static CopyMemoryDelegate BuildCopyMemoryMethod()
        {
            var methodCopyMemory = new DynamicMethod("CopyMemory<" + typeof(T).FullName + ">",
                typeof(void), new[] { typeof(T).MakeByRefType(), typeof(byte).MakeByRefType(), typeof(int) }, typeof(FastStruct<T>));

            ILGenerator generator = methodCopyMemory.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Cpblk);
            generator.Emit(OpCodes.Ret);

            return (CopyMemoryDelegate)methodCopyMemory.CreateDelegate(typeof(CopyMemoryDelegate));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArrayToStructure(byte[] src)
        {
            return LoadFromByteRef(ref src[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArrayToStructure(byte[] src, int offset)
        {
            return LoadFromByteRef(ref src[offset]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArrayToStructure(ref byte src)
        {
            return LoadFromByteRef(ref src);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ReadArray(byte[] source)
        {
            T[] buffer = new T[source.Length / Size];

            if (source.Length > 0)
                CopyMemory(ref buffer[0], ref source[0], source.Length);

            return buffer;
        }
    }
}
