using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminAccountsService
    {
        Task<AccountsFilterResult> GetStudentsAsync(AccountsFilters filters, CancellationToken ct);
        Task<AccountsFilterResult> GetTeachersAsync(AccountsFilters filters, CancellationToken ct);

        /// <summary>
        /// Changes a user's account status (e.g. Active / Inactive) and logs a My Activity notification.
        /// </summary>
        Task<bool> SuspendAccount(
            int adminUserId,
            int userId,
            string status,
            string? roleLabel,
            CancellationToken ct);
    }
}
