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

        [HttpGet("/Calendar")]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin")) return RedirectToRoute("AdminCalendar");
            if (User.IsInRole("Teacher")) return RedirectToRoute("TeacherCalendar");
            return RedirectToRoute("StudentCalendar");
        }

        private async Task<IActionResult> BuildCalendarViewAsync(DateTime? from, DateTime? to)
        {
            ViewData["PageHeader"] = "Calendar";

            var start = from ?? DateTime.UtcNow.AddDays(-30);
            var end = to ?? DateTime.UtcNow.AddDays(30);

            var model = await _calendarService.GetUserCalendarAsync(User, start, end);
            return View("~/Views/Shared/Partials/Calendar.cshtml", model);
        }

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
                start = ev.StartUtc,
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

        [HttpPost("/Calendar/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CalendarEventCreateVm input)
        {
            try
            {
                await _calendarService.CreateAsync(User, input);
                TempData["ToastSuccess"] = "Event added successfully.";
                return RedirectToRoleCalendar();
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = ex.Message;
                return RedirectToRoleCalendar();
            }
        }

        [HttpPost("/Calendar/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] CalendarEventUpdateVm input)
        {
            try
            {
                await _calendarService.UpdateAsync(User, input);
                TempData["ToastSuccess"] = "Event updated successfully.";
                return RedirectToRoleCalendar();
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = ex.Message;
                return RedirectToRoleCalendar();
            }
        }

        [HttpPost("/Calendar/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _calendarService.DeleteAsync(User, id);
                TempData["ToastSuccess"] = "Event deleted successfully.";
                return RedirectToRoleCalendar();
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = ex.Message;
                return RedirectToRoleCalendar();
            }
        }

        private IActionResult RedirectToRoleCalendar()
        {
            if (User.IsInRole("Admin")) return RedirectToRoute("AdminCalendar");
            if (User.IsInRole("Teacher")) return RedirectToRoute("TeacherCalendar");
            return RedirectToRoute("StudentCalendar");
        }
    }
}
