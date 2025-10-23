using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Diagnostics
{
    public static class Logger
    {
        private static readonly object _lock = new();
        public static void WriteLine(string msg)
        {
            lock (_lock)
            {
                Console.WriteLine(msg);
            }
        }
    }

}
