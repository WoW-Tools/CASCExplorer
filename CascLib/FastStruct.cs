using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;

namespace CASCExplorer
{
    internal class UnsafeNativeMethods
    {
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        [SecurityCritical]
        internal static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        [SecurityCritical]
        internal static extern unsafe void CopyMemoryPtr(void* dest, void* src, uint count);
    }

    public static class FastStruct<T> where T : struct
    {
        private delegate IntPtr GetPtrDelegate(ref T value);
        private delegate T PtrToStructureDelegateIntPtr(IntPtr pointer);
        private unsafe delegate T PtrToStructureDelegateBytePtr(byte* pointer);

        private readonly static GetPtrDelegate GetPtr = BuildGetPtrMethod();
        private readonly static PtrToStructureDelegateIntPtr PtrToStructureIntPtr = BuildLoadFromIntPtrMethod();
        private readonly static PtrToStructureDelegateBytePtr PtrToStructureBytePtr = BuildLoadFromBytePtrMethod();

        public static readonly int Size = Marshal.SizeOf<T>();

        private static DynamicMethod methodGetPtr;
        private static DynamicMethod methodLoadIntPtr;
        private static DynamicMethod methodLoadBytePtr;

        private static GetPtrDelegate BuildGetPtrMethod()
        {
            methodGetPtr = new DynamicMethod("GetStructPtr<" + typeof(T).FullName + ">",
                typeof(IntPtr), new[] { typeof(T).MakeByRefType() }, typeof(FastStruct<T>));

            ILGenerator generator = methodGetPtr.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Conv_U);
            generator.Emit(OpCodes.Ret);
            return (GetPtrDelegate)methodGetPtr.CreateDelegate(typeof(GetPtrDelegate));
        }

        private static PtrToStructureDelegateIntPtr BuildLoadFromIntPtrMethod()
        {
            methodLoadIntPtr = new DynamicMethod("PtrToStructureIntPtr<" + typeof(T).FullName + ">",
                typeof(T), new[] { typeof(IntPtr) }, typeof(FastStruct<T>));

            ILGenerator generator = methodLoadIntPtr.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (PtrToStructureDelegateIntPtr)methodLoadIntPtr.CreateDelegate(typeof(PtrToStructureDelegateIntPtr));
        }

        private static PtrToStructureDelegateBytePtr BuildLoadFromBytePtrMethod()
        {
            methodLoadBytePtr = new DynamicMethod("PtrToStructureBytePtr<" + typeof(T).FullName + ">",
                typeof(T), new[] { typeof(byte*) }, typeof(FastStruct<T>));

            ILGenerator generator = methodLoadBytePtr.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (PtrToStructureDelegateBytePtr)methodLoadBytePtr.CreateDelegate(typeof(PtrToStructureDelegateBytePtr));
        }

        public static T PtrToStructure(IntPtr ptr)
        {
            return PtrToStructureIntPtr(ptr);
        }

        public static unsafe T PtrToStructure(byte* ptr)
        {
            return PtrToStructureBytePtr(ptr);
        }

        public static T[] ReadArray(IntPtr source, int bytesCount)
        {
            uint elementSize = (uint)Size;

            T[] buffer = new T[bytesCount / elementSize];

            if (bytesCount > 0)
            {
                IntPtr p = GetPtr(ref buffer[0]);
                UnsafeNativeMethods.CopyMemory(p, source, (uint)bytesCount);
            }

            return buffer;
        }
    }
}
