using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Student,Teacher,Admin")]
    public class CalendarController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ICalendarService _calendarService;
        private readonly IRightSidebarService _rightSidebar; // ✅ inject

        public CalendarController(
            IProfileService profileService,
            ICalendarService calendarService,
            IRightSidebarService rightSidebar) // ✅ inject
        {
            _profileService = profileService;
            _calendarService = calendarService;
            _rightSidebar = rightSidebar;
        }

        // small helper so we don’t forget to SSR the sidebar
        private async Task SetRightSidebarAsync()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                ViewData["RightSidebar"] =
                    await _rightSidebar.BuildAsync(User, takeNotifications: 5, takeEvents: 5);
            }
        }

        // --------------------------------------------------------------------
        // Canonical GET routes per role (Student / Teacher / Admin)
        // --------------------------------------------------------------------

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

        // --------------------------------------------------------------------
        // Shared builder used by all three
        // --------------------------------------------------------------------
        private async Task<IActionResult> BuildCalendarViewAsync(DateTime? from, DateTime? to)
        {
            ViewData["PageHeader"] = "Calendar";

            // ✅ SSR the right sidebar for this page, too
            await SetRightSidebarAsync();

            // Default range = ±30 days
            var start = from ?? DateTime.UtcNow.AddDays(-30);
            var end = to ?? DateTime.UtcNow.AddDays(30);

            var model = await _calendarService.GetUserCalendarAsync(User, start, end);
            return View("~/Views/Shared/Partials/Calendar.cshtml", model);
        }

        // --------------------------------------------------------------------
        // JSON Feed for FullCalendar
        // --------------------------------------------------------------------
        [HttpGet("/Calendar/Feed")]
        public async Task<IActionResult> Feed(DateTime start, DateTime end)
        {
            var model = await _calendarService.GetUserCalendarAsync(User, start, end);
            return Json(model.Events);
        }

        // --------------------------------------------------------------------
        // CRUD: Create / Update / Delete
        // --------------------------------------------------------------------
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
