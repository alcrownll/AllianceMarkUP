using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize] // users must be logged in
    public class PartialsController : Controller
    {
        private readonly IRightSidebarService _rightSidebar;
        private readonly ICalendarService _calendar;

        public PartialsController(IRightSidebarService rightSidebar, ICalendarService calendar)
        {
            _rightSidebar = rightSidebar;
            _calendar = calendar;
        }

        // ===== Existing endpoints you already have =====
        [HttpGet]
        public IActionResult RightSidebar()
        {
            return ViewComponent("RightSidebar", new { takeNotifications = 5, takeEvents = 5 });
        }

        // You can keep this if some page still loads a calendar *partial*.
        [HttpGet]
        public async Task<IActionResult> Calendar(DateTime? fromUtc, DateTime? toUtc)
        {
            var from = fromUtc ?? DateTime.UtcNow.AddDays(-15);
            var to = toUtc ?? DateTime.UtcNow.AddDays(45);
            var vm = await _calendar.GetUserCalendarAsync(User, from, to);
            return PartialView("~/Views/Shared/Partials/_Calendar.cshtml", vm);
        }
    }
}
