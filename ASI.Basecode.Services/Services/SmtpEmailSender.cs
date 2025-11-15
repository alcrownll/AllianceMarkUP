using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    public SmtpEmailSender(IConfiguration cfg) => _cfg = cfg;

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _cfg["Smtp:Host"];
        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = _cfg["Smtp:User"];
        var pass = _cfg["Smtp:Pass"];
        var from = string.IsNullOrWhiteSpace(_cfg["Smtp:From"]) ? user : _cfg["Smtp:From"];

        using var client = new SmtpClient(host, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true,
            Timeout = 20000 // 20s
        };

    
        
        client.DeliveryFormat = SmtpDeliveryFormat.International;

        using var msg = new MailMessage(from, to)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        try
        {
            await client.SendMailAsync(msg);
        }
        catch (SmtpException ex)
        {
            // Surface useful SMTP codes/messages in logs
            throw new InvalidOperationException(
                $"SMTP failed. StatusCode={(int)ex.StatusCode} ({ex.StatusCode}). " +
                $"Host={host}:{port}. AuthUser={user}. " +
                $"Inner={ex.InnerException?.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SMTP failed with non-SMTP exception. Host={host}:{port}. AuthUser={user}. " +
                $"Inner={ex.InnerException?.Message}", ex);
        }
    }
}
