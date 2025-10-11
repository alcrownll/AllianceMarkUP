using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class CalendarController : Controller
    {
        private readonly ICalendarService _svc;
        public CalendarController(ICalendarService svc) => _svc = svc;

        [HttpGet("")]
        public async Task<IActionResult> Index(DateTime? startUtc, DateTime? endUtc)
        {
            DateTime start = startUtc.HasValue
                ? DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc)
                : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            DateTime end = endUtc.HasValue
                ? DateTime.SpecifyKind(endUtc.Value, DateTimeKind.Utc)
                : start.AddMonths(1).AddTicks(-1);

            var vm = await _svc.GetUserCalendarAsync(User, start, end);
            return View("~/Views/Shared/Partials/Calendar.cshtml", vm);
        }

        // FullCalendar event feed (normalize to UTC)
        [HttpGet("Feed")]
        public async Task<IActionResult> Feed(DateTimeOffset start, DateTimeOffset end)
        {
            var vm = await _svc.GetUserCalendarAsync(User, start.UtcDateTime, end.UtcDateTime);

            var json = vm.Events.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                start = DateTime.SpecifyKind(e.StartUtc, DateTimeKind.Utc),
                end = DateTime.SpecifyKind(e.EndUtc, DateTimeKind.Utc),
                allDay = e.IsAllDay,
                extendedProps = new
                {
                    location = e.Location,
                    isGlobal = e.IsGlobal,
                    canEdit = e.CanEdit
                }
            });
            return Json(json);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CalendarEventCreateVm input)
        {
            if (!ModelState.IsValid)
            {
                TempData["CalendarError"] = string.Join("; ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            var created = await _svc.CreateAsync(User, input);

            // Use created (not updated) and emit UTC with Z via DateTimeOffset
            var m0 = new DateTimeOffset(
                new DateTime(created.StartUtc.Year, created.StartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc));
            var m1 = m0.AddMonths(1).AddTicks(-1);

            TempData["CalendarOk"] = "Event created.";
            return RedirectToAction(nameof(Index), new { startUtc = m0, endUtc = m1 });
        }

        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] CalendarEventUpdateVm input)
        {
            if (!ModelState.IsValid)
            {
                TempData["CalendarError"] = string.Join("; ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            var updated = await _svc.UpdateAsync(User, input);
            if (updated == null)
            {
                TempData["CalendarError"] = "Unable to update (not found or no permission).";
                return RedirectToAction(nameof(Index));
            }

            var m0 = new DateTimeOffset(
                new DateTime(updated.StartUtc.Year, updated.StartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc));
            var m1 = m0.AddMonths(1).AddTicks(-1);

            TempData["CalendarOk"] = "Event updated.";
            return RedirectToAction(nameof(Index), new { startUtc = m0, endUtc = m1 });
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(User, id);
            TempData["CalendarOk"] = "Event deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
