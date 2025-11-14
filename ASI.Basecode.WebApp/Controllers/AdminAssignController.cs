using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AdminAssignController : Controller
    {
        private readonly IAdminAssignService _service;
        public AdminAssignController(IAdminAssignService service) => _service = service;

        public async Task<IActionResult> Index(string q, CancellationToken ct = default)
        {
            var model = await _service.GetListAsync(q);
            ViewData["Query"] = q ?? "";
            return View("~/Views/Admin/AdminAssign.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> Assign(
            string blockProgram,
            string blockYear,
            string blockSection,
            string status = "Active",
            int page = 1,
            int pageSize = 10,
            string mode = "block",
            CancellationToken ct = default)
        {
            ViewBag.Courses = await _service.GetCoursesAsync();
            ViewBag.Programs = await _service.GetProgramsAsync();
            ViewBag.Teachers = await _service.GetTeachersWithUsersAsync();

            var paged = await _service.GetStudentsForAssignAsync(
                blockProgram, blockYear, blockSection, status, page, pageSize, ct);

            ViewBag.Students = paged.Items;
            ViewBag.Total = paged.TotalCount;
            ViewBag.Page = paged.Page;
            ViewBag.PageSize = 10;

            ViewBag.BlockProgram = blockProgram;
            ViewBag.BlockYear = blockYear;
            ViewBag.BlockSection = blockSection;
            ViewBag.FilterStatus = status;
            ViewBag.Mode = string.IsNullOrWhiteSpace(mode) ? "block" : mode;

            return View("~/Views/Admin/AdminAssignPhase2.cshtml", new AssignedCourse());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(
            AssignedCourse model,
            string blockProgram,
            string blockYear,
            string blockSection,
            string mode,
            [FromForm] int[] SelectedStudentIds,

            [FromForm] string ScheduleRoom,
            [FromForm] string ScheduleStart,
            [FromForm] string ScheduleEnd,
            [FromForm] string ScheduleDaysCsv,
            CancellationToken ct)
        {
            IEnumerable<int> extras = SelectedStudentIds ?? new int[0];

            var id = await _service.CreateAssignedCourseAsync(
                model,
                blockProgram,
                blockYear,
                blockSection,
                extras,
                ScheduleRoom,
                ScheduleStart,
                ScheduleEnd,
                ScheduleDaysCsv,
                ct);

            TempData["Ok"] = $"Assigned course created (ID #{id}).";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ManualTable(
            string blockProgram,
            string blockYear,
            string blockSection,
            string status = "Active",
            int page = 1,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            var paged = await _service.GetStudentsForAssignAsync(
                blockProgram, blockYear, blockSection, status, page, pageSize, ct);

            ViewBag.Page = paged.Page;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.Total = paged.TotalCount;
            ViewBag.BlockProgram = blockProgram ?? "";
            ViewBag.BlockYear = blockYear ?? "";
            ViewBag.BlockSection = blockSection ?? "";
            ViewBag.FilterStatus = status ?? "Active";

            return PartialView("~/Views/Admin/Partials/_ManualStudentsTable.cshtml", paged.Items);
        }

        [HttpGet]
        public async Task<IActionResult> View(int id, CancellationToken ct = default)
        {
            var ac = await _service.GetAssignedCourseAsync(id, ct);
            if (ac == null) return NotFound();

            ViewBag.Courses = await _service.GetCoursesAsync();
            ViewBag.Programs = await _service.GetProgramsAsync();
            ViewBag.Teachers = await _service.GetTeachersWithUsersAsync();
            ViewBag.EnrolledStudents = await _service.GetEnrolledStudentsAsync(id, ct);
            ViewBag.Schedules = await _service.GetSchedulesAsync(id, ct);

            return View("~/Views/Admin/AdminAssignView.cshtml", ac);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
    AssignedCourse model,
    [FromForm] int[] RemoveStudentIds,
    [FromForm] int[] SelectedStudentIds,
    [FromForm] string ScheduleRoom,
    [FromForm] string ScheduleStart,
    [FromForm] string ScheduleEnd,
    [FromForm] string ScheduleDaysCsv,
    CancellationToken ct)
        {
            try
            {
                await _service.UpdateAssignedCourseAsync(
                    model,
                    RemoveStudentIds,
                    SelectedStudentIds,
                    ScheduleRoom,
                    ScheduleStart,
                    ScheduleEnd,
                    ScheduleDaysCsv,
                    ct);

                TempData["Ok"] = "Assigned course updated.";
                return RedirectToAction(nameof(View), new { id = model.AssignedCourseId });
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);

                ViewBag.Courses = await _service.GetCoursesAsync();
                ViewBag.Programs = await _service.GetProgramsAsync();
                ViewBag.Teachers = await _service.GetTeachersWithUsersAsync();
                ViewBag.EnrolledStudents = await _service.GetEnrolledStudentsAsync(model.AssignedCourseId, ct);
                ViewBag.Schedules = await _service.GetSchedulesAsync(model.AssignedCourseId, ct);

                return View("~/Views/Admin/AdminAssignView.cshtml", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> AddStudentsTable(
            int id,
            string blockProgram,
            string blockYear,
            string blockSection,
            string status = "Active",
            int page = 1,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            var (items, total) = await _service.GetAddableStudentsPageAsync(
                id, blockProgram, blockYear, blockSection, status, page, pageSize, ct);

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;

            return PartialView("~/Views/Admin/Partials/_ManualStudentsTable.cshtml", items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var (ok, message) = await _service.DeleteAssignedCourseAsync(id, ct);
            if (ok) TempData["Ok"] = message;
            else TempData["Error"] = message;

            return RedirectToAction(nameof(Index));
        }
    }
}
