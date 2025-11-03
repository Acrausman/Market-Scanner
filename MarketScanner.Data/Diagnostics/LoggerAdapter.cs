namespace MarketScanner.Data.Diagnostics
{
    public sealed class LoggerAdapter : ILogger
    {
        public void Info(string message) => Logger.Info(message);
        public void Warn(string message) => Logger.Warn(message);
        public void Error(string message) => Logger.Error(message);
        public void Debug(string message) => Logger.Debug(message);
    }
}
