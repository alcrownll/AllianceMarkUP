using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Server-side widget that builds the right sidebar each time the layout renders.
    /// Invoke from layout: @await Component.InvokeAsync("RightSidebar", new { takeNotifications = 5, takeEvents = 5 })
    /// </summary>
    public sealed class RightSidebarViewComponent : ViewComponent
    {
        private readonly IRightSidebarService _rightSidebar;

        public RightSidebarViewComponent(IRightSidebarService rightSidebar)
        {
            _rightSidebar = rightSidebar;
        }

        public async Task<IViewComponentResult> InvokeAsync(int takeNotifications = 5, int takeEvents = 5)
        {
            // Skip if not authenticated
            if (HttpContext?.User?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            RightSidebarViewModel vm = await _rightSidebar.BuildAsync(HttpContext.User, takeNotifications, takeEvents);

            // Looks for Views/Shared/Components/RightSidebar/Default.cshtml
            return View(vm);
        }
    }
}
