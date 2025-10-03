using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminAccountsService
    {
        Task<AccountsFilterResult> GetStudentsAsync(AccountsFilters filters, CancellationToken ct);
        Task<AccountsFilterResult> GetTeachersAsync(AccountsFilters filters, CancellationToken ct);
    }
}
