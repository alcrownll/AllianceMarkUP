using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize] // users must be logged in to fetch these
    public class PartialsController : Controller
    {
        private readonly IRightSidebarService _rightSidebar;
        private readonly ICalendarService _calendar;

        public PartialsController(IRightSidebarService rightSidebar, ICalendarService calendar)
        {
            _rightSidebar = rightSidebar;
            _calendar = calendar;
        }

        [HttpGet]
        public async Task<IActionResult> RightSidebar()
        {
            var vm = await _rightSidebar.BuildAsync(User, takeNotifications: 5);
            return PartialView("~/Views/Shared/Sidebars/_RightSidebar.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Calendar(DateTime? fromUtc, DateTime? toUtc)
        {
            // default: 30-day window around today
            var from = fromUtc ?? DateTime.UtcNow.AddDays(-15);
            var to = toUtc ?? DateTime.UtcNow.AddDays(45);

            var vm = await _calendar.GetUserCalendarAsync(User, from, to);
            return PartialView("~/Views/Shared/Partials/_Calendar.cshtml", vm);
        }
    }
}
