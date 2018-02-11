using CASCLib;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CASCExplorer
{
    class CASCEntrySorter : IComparer<ICASCEntry>
    {
        public int SortColumn { get; set; }
        public SortOrder Order { get; set; }
        public CASCHandler CASC { get; set; }

        public CASCEntrySorter()
        {
            SortColumn = 0;
            Order = SortOrder.Ascending;
        }

        public int Compare(ICASCEntry x, ICASCEntry y)
        {
            int result = x.CompareTo(y, SortColumn, CASC);

            if (Order == SortOrder.Ascending)
                return result;
            else
                return -result;
        }
    }
}
