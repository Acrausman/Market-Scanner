using System;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;

namespace MarketScanner.Data.Services
{
    public class EmailService
    {
        public void SendEmail(string from, string to, string subject, string body)
        {
            try
            {
                Console.WriteLine($"[Email] To: {to} | Subject: {subject} | Message: {body}");
                Debug.WriteLine($"[Email] To: {to} | Subject: {subject} | Message: {body}");

                using var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential("adam.crausman@invest3c.com", "xicc tthg tull csfd\r\n"),
                    EnableSsl = true
                };

                var mail = new MailMessage("adam.crausman@invest3c.com", to, subject, body);
                client.Send(mail);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Error] {ex.Message}");
            }
        }
    }
}
