// Services/Interfaces/IAccountService.cs
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAccountService
    {
        Task<(bool ok, string message)> ChangePasswordAsync(
            int userId,
            string oldPassword,
            string newPassword,
            CancellationToken ct = default);
    }
}
