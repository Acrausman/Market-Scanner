using System;

namespace MarketScanner.Data.Diagnostics
{
    public static class Logger
    {
        private static readonly object _lock = new();

        public static void Info(string message) => Write(message);

        public static void Warn(string message) => Write(message);

        public static void Error(string message) => Write(message);

        public static void Debug(string message) => Write(message);

        public static void WriteLine(string msg) => Write(msg);

        private static void Write(string message)
        {
            lock (_lock)
            {
                Console.WriteLine(message);
            }
        }
    }
}
