using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core
{
    public class Engine
    {
        public event EventHandler<string>? TriggerHit;

        public void start()
        {
            Task.Delay(1000).ContinueWith(_ =>
            {
                TriggerHit?.Invoke(this, "AAPL RSI Trigger Fired!");
            });
        }
    }
}
