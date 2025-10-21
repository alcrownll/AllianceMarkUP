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
    [Authorize(Roles = "Student,Teacher,Admin")]
    public class CalendarController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ICalendarService _calendarService;

        public CalendarController(
            IProfileService profileService,
            ICalendarService calendarService
        )
        {
            _profileService = profileService;
            _calendarService = calendarService;
        }

        // ------------------------------------------------------------
        // Canonical GET routes per role (Student / Teacher / Admin)
        // ------------------------------------------------------------
        [HttpGet("/Student/Calendar", Name = "StudentCalendar")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentIndex(DateTime? from = null, DateTime? to = null)
            => await BuildCalendarViewAsync(from, to);

        [HttpGet("/Teacher/Calendar", Name = "TeacherCalendar")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> TeacherIndex(DateTime? from = null, DateTime? to = null)
            => await BuildCalendarViewAsync(from, to);

        [HttpGet("/Admin/Calendar", Name = "AdminCalendar")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminIndex(DateTime? from = null, DateTime? to = null)
            => await BuildCalendarViewAsync(from, to);

        // A universal landing that redirects to the correct role route.
        [HttpGet("/Calendar")]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin")) return RedirectToRoute("AdminCalendar");
            if (User.IsInRole("Teacher")) return RedirectToRoute("TeacherCalendar");
            return RedirectToRoute("StudentCalendar");
        }

        // ------------------------------------------------------------
        // Shared builder used by all three
        // ------------------------------------------------------------
        private async Task<IActionResult> BuildCalendarViewAsync(DateTime? from, DateTime? to)
        {
            ViewData["PageHeader"] = "Calendar";

            // Default range = ±30 days
            var start = from ?? DateTime.UtcNow.AddDays(-30);
            var end = to ?? DateTime.UtcNow.AddDays(30);

            var model = await _calendarService.GetUserCalendarAsync(User, start, end);
            return View("~/Views/Shared/Partials/Calendar.cshtml", model);
        }

        // ------------------------------------------------------------
        // JSON Feed for FullCalendar
        // ------------------------------------------------------------
        // IMPORTANT: FullCalendar sends ISO strings with timezone. Accept string, parse robustly,
        // and return objects with keys: id, title, start, end, allDay, extendedProps.
        [HttpGet("/Calendar/Feed")]
        public async Task<IActionResult> Feed(string start, string end)
        {
            if (!DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var s))
                s = DateTimeOffset.UtcNow.AddMonths(-1);

            if (!DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var e))
                e = DateTimeOffset.UtcNow.AddMonths(2);

            var model = await _calendarService.GetUserCalendarAsync(User, s.UtcDateTime, e.UtcDateTime);

            var events = model.Events.Select(ev => new
            {
                id = ev.Id.ToString(),
                title = ev.Title,
                start = ev.StartUtc, // UTC timestamps are fine; FC renders in local (timeZone:'local')
                end = ev.EndUtc,
                allDay = ev.IsAllDay,
                extendedProps = new
                {
                    location = ev.Location,
                    isGlobal = ev.IsGlobal,
                    canEdit = ev.CanEdit
                }
            });

            return Json(events);
        }

        // ------------------------------------------------------------
        // CRUD: Create / Update / Delete
        // ------------------------------------------------------------
        [HttpPost("/Calendar/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CalendarEventCreateVm input)
        {
            try
            {
                await _calendarService.CreateAsync(User, input);
                return RedirectToRoleCalendar();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToRoleCalendar();
            }
        }

        [HttpPost("/Calendar/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(CalendarEventUpdateVm input)
        {
            try
            {
                await _calendarService.UpdateAsync(User, input);
                return RedirectToRoleCalendar();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToRoleCalendar();
            }
        }

        [HttpPost("/Calendar/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _calendarService.DeleteAsync(User, id);
            return RedirectToRoleCalendar();
        }

        private IActionResult RedirectToRoleCalendar()
        {
            if (User.IsInRole("Admin")) return RedirectToRoute("AdminCalendar");
            if (User.IsInRole("Teacher")) return RedirectToRoute("TeacherCalendar");
            return RedirectToRoute("StudentCalendar");
        }
    }
}
