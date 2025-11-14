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
        /// Suspends a user account and logs a My Activity notification for the acting admin.
        /// </summary>
        /// <param name="adminUserId">The admin performing the action.</param>
        /// <param name="userId">The user being suspended.</param>
        /// <param name="status">Target status (e.g. "Inactive").</param>
        /// <param name="roleLabel">"student", "teacher", etc. (for message wording).</param>
        Task<bool> SuspendAccount(
            int adminUserId,
            int userId,
            string status,
            string? roleLabel,
            CancellationToken ct);
    }
}
