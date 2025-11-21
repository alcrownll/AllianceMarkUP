using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize] // users must be logged in
    public class PartialsController : Controller
    {
        private readonly IRightSidebarService _rightSidebar;
        private readonly ICalendarService _calendar;
        private readonly INotificationService _notifications;
        private readonly IProfileService _profile;

        public PartialsController(
            IRightSidebarService rightSidebar,
            ICalendarService calendar,
            INotificationService notifications,
            IProfileService profile)
        {
            _rightSidebar = rightSidebar;
            _calendar = calendar;
            _notifications = notifications;
            _profile = profile;
        }

        // existing: returns the sidebar partial (server-rendered)
        [HttpGet]
        public IActionResult RightSidebar()
        {
            return ViewComponent("RightSidebar", new { takeNotifications = 5, takeEvents = 5 });
        }

        // existing: returns the calendar partial
        [HttpGet]
        public async Task<IActionResult> Calendar(DateTime? fromUtc, DateTime? toUtc)
        {
            var from = fromUtc ?? DateTime.UtcNow.AddDays(-15);
            var to = toUtc ?? DateTime.UtcNow.AddDays(45);
            var vm = await _calendar.GetUserCalendarAsync(User, from, to);
            return PartialView("~/Views/Shared/Partials/_Calendar.cshtml", vm);
        }

    
        [HttpGet]
        public IActionResult LatestActivity(int take = 5)
        {
            var userId = _profile.GetCurrentUserId();

            var items = _notifications.GetLatestActivity(userId, take)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    when = n.When,
                    isRead = n.IsRead
                })
                .ToList();

            return Json(new { ok = true, items });
        }
    }
}
