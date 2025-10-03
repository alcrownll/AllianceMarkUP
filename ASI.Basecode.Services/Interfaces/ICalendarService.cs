using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICalendarService
    {
        Task<CalendarViewModel> GetUserCalendarAsync(ClaimsPrincipal user, DateTime fromUtc, DateTime toUtc);
        Task<CalendarEventVm> CreateAsync(ClaimsPrincipal user, CalendarEventVm dto, bool isAdminCreatesGlobal);
        Task<CalendarEventVm> UpdateAsync(ClaimsPrincipal user, CalendarEventVm dto);
        Task DeleteAsync(ClaimsPrincipal user, int id);
    }
}
