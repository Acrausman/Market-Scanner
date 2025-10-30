namespace MarketScanner.Data.Services
{
    public interface IAlertSink
    {
        void AddAlert(string message);
    }
}
