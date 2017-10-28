using System;
using System.IO;

namespace CASCLib
{
    public class Logger
    {
        static FileStream fs = new FileStream("debug.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        static StreamWriter logger = new StreamWriter(fs) { AutoFlush = true };

        public static void WriteLine(string format, params object[] args)
        {
            logger.Write("[{0}]: ", DateTime.Now);
            logger.WriteLine(format, args);
        }
    }
}
