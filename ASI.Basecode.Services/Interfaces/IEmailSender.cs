using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }
}
