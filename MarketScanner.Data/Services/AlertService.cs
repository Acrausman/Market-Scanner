using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    internal class AlertService
    {
        public Task SendEmailAsync(string to, string subject, string message)
        {
            // TODO: implement email send
            return Task.CompletedTask;
        }

        public Task SendSmsAsync(string to, string message)
        {
            // TODO: Implement SMS service
            return Task.CompletedTask;
        }
    }
}
