using System.Collections.Generic;

namespace CASCExplorer
{
    class MD5HashComparer : IEqualityComparer<MD5Hash>
    {
        const uint FnvPrime32 = 16777619;
        const uint FnvOffset32 = 2166136261;

        public unsafe bool Equals(MD5Hash x, MD5Hash y)
        {
            for (int i = 0; i < 16; ++i)
                if (x.Value[i] != y.Value[i])
                    return false;

            return true;
        }

        public int GetHashCode(MD5Hash obj)
        {
            return To32BitFnv1aHash(obj);
        }

        private unsafe int To32BitFnv1aHash(MD5Hash toHash)
        {
            uint hash = FnvOffset32;

            uint* ptr = (uint*)&toHash;

            for (int i = 0; i < 4; i++)
            {
                hash ^= ptr[i];
                hash *= FnvPrime32;
            }

            return unchecked((int)hash);
        }
    }
}
