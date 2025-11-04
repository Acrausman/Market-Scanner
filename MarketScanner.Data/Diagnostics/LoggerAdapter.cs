using System;
using MarketScanner.Core.Abstractions;

namespace MarketScanner.Data.Diagnostics
{
    public sealed class LoggerAdapter : IAppLogger
    {
        public void Log(LogSeverity severity, string message, Exception? exception = null)
        {
            var formatted = exception is null ? message : $"{message} :: {exception}";

            switch (severity)
            {
                case LogSeverity.Debug:
                    Logger.Debug(formatted);
                    break;
                case LogSeverity.Warning:
                    Logger.Warn(formatted);
                    break;
                case LogSeverity.Error:
                    Logger.Error(formatted);
                    break;
                default:
                    Logger.Info(formatted);
                    break;
            }
        }
    }
}
