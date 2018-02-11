using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CASCExplorer
{
    class PassEnumerator
    {
        private int length;
        private char[] chars;
        private int[] indexes;
        private StringBuilder result;

        public long TotalCount;
        public long Processed;

        public PassEnumerator(string charStr, int length)
        {
            this.length = length;
            chars = charStr.ToCharArray();
            result = new StringBuilder(length);
            indexes = new int[length];

            TotalCount = 1;

            for (var j = 0; j < length; j++)
            {
                result.Insert(j, chars[0]);
                TotalCount *= chars.Length;
            }
        }

        public IEnumerable<string> Enumerate()
        {
            int i = 0;

            do
            {
                yield return result.ToString();

                Processed++;

                for (i = 0; i < length; i++)
                {
                    var ind = indexes[i];
                    ind++;
                    if (ind >= chars.Length)
                    {
                        indexes[i] = 0;
                        result[i] = chars[0];
                    }
                    else
                    {
                        indexes[i] = ind;
                        result[i] = chars[ind];
                        break;
                    }
                }
            } while (i < length);
        }
    }

    public class Bruteforce : IEnumerable<string>
    {
        private StringBuilder sb = new StringBuilder();
        public string charset = "abcdefghijklmnopqrstuvwxyz-_ ";
        private uint len;
        private Stopwatch sw = new Stopwatch();
        public int max { get; set; }
        public int min { get; set; }
        public ulong total { get; set; }
        public ulong done { get; set; }

        public ulong speed
        {
            get
            {
                ulong totalSec = (ulong)sw.Elapsed.TotalSeconds;
                if (totalSec == 0 || done == 0)
                    return 0;
                return done / totalSec;
            }
        }

        public TimeSpan eta
        {
            get
            {
                if (speed == 0)
                    return TimeSpan.Zero;
                return TimeSpan.FromSeconds((total - done) / speed);
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            sw.Start();

            len = (uint)charset.Length;

            for (int x = min; x <= max; x++)
            {
                ulong c = (ulong)Math.Pow(len, x);
                total += c;

                for (ulong i = 0; i < c; ++i, ++done)
                {
                    yield return Factoradic(i, x - 1);
                }
            }

            sw.Stop();
        }

        private string Factoradic(ulong l, int power)
        {
            sb.Clear();
            while (power >= 0)
            {
                sb = sb.Append(charset[(int)(l % len)]);
                l /= len;
                power--;
            }
            return sb.ToString();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
