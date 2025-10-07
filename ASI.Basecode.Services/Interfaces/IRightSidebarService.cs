using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IRightSidebarService
    {
        Task<RightSidebarViewModel> BuildAsync(ClaimsPrincipal user, int takeNotifications = 5, int takeEvents = 5);
    }
}
