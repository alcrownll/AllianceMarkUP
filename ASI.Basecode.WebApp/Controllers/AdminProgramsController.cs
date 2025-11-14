// ASI.Basecode.WebApp/Controllers/AdminProgramsController.cs
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [ApiController]
    [Route("admin/programs")]
    public class AdminProgramsController : Controller
    {
        private readonly ICurriculumService _svc;
        private readonly IProfileService _profileService;

        public AdminProgramsController(
            ICurriculumService svc,
            IProfileService profileService)
        {
            _svc = svc;
            _profileService = profileService;
        }

        private int CurrentAdminUserId() => _profileService.GetCurrentUserId();

        [HttpGet("list")]
        public IActionResult List([FromQuery] string q = null)
        {
            var items = _svc.ListPrograms(q);
            return PartialView("~/Views/Admin/Partials/_ProgramsTable.cshtml", items);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public IActionResult Create([FromForm] string code, [FromForm] string name, [FromForm] string notes)
        {
            var adminUserId = CurrentAdminUserId();
            var p = _svc.CreateProgram(code, name, notes, adminUserId);

            return Json(new { ok = true, programId = p.ProgramId, code = p.ProgramCode, name = p.ProgramName });
        }

        [HttpPost("add-course")]
        [ValidateAntiForgeryToken]
        public IActionResult AddCourse(
            [FromForm] int programId,
            [FromForm] int year,
            [FromForm] int term,
            [FromForm] int courseId,
            [FromForm] int prereqCourseId)
        {
            _svc.AddCourseToTerm(programId, year, term, courseId, prereqCourseId);
            return Json(new { ok = true });
        }

        [HttpGet("{programId}/term")]
        public IActionResult GetTerm(int programId, [FromQuery] int year, [FromQuery] int term)
        {
            var items = _svc.GetTerm(programId, year, term).Select(pc => new
            {
                id = pc.ProgramCourseId,
                code = pc.Course?.CourseCode,
                title = pc.Course?.Description,
                lec = pc.Course?.LecUnits ?? 0,
                lab = pc.Course?.LabUnits ?? 0,
                units = (pc.Course?.LecUnits ?? 0) + (pc.Course?.LabUnits ?? 0),
                prereq = pc.PrerequisiteCourse?.CourseCode ?? "—"
            });
            return Json(new { ok = true, items });
        }

        [HttpPost("remove-course")]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveCourse([FromForm] int programCourseId)
        {
            _svc.RemoveProgramCourse(programCourseId);
            return Json(new { ok = true });
        }

        [ValidateAntiForgeryToken]
        [HttpPost("{programId:int}/term/item/{programCourseId:int}/remove")]
        public IActionResult RemoveTermItem(int programId, int programCourseId)
        {
            _svc.RemoveProgramCourse(programCourseId);
            return Json(new { ok = true });
        }

        [HttpPost("cancel")]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(
            [FromForm] int programId,
            [FromServices] ICurriculumService svc,
            [FromForm] bool force = false)
        {
            if (programId <= 0) return BadRequest(new { ok = false, message = "Invalid programId." });

            var hasCourses = svc.HasAnyCourses(programId);
            if (hasCourses && !force)
                return Json(new { ok = true, hasCourses = true });

            // Cancel draft program – no notification needed; use legacy DiscardProgram
            svc.DiscardProgram(programId);
            return Json(new { ok = true, deleted = true });
        }

        [ValidateAntiForgeryToken]
        [HttpPost("{programId:int}/term/bulk-add")]
        public IActionResult BulkAddToTerm(
            int programId,
            [FromForm] int year,
            [FromForm] int term,
            [FromForm] int[] courseIds,
            [FromForm] int[] prereqIds)
        {
            if (programId <= 0 || year is < 1 or > 4 || term is < 1 or > 2 || courseIds == null)
                return BadRequest(new { ok = false, message = "Invalid payload." });

            _svc.AddCoursesToTermBulk(programId, year, term, courseIds, prereqIds ?? Array.Empty<int>());

            var items = _svc.GetTerm(programId, year, term).Select(pc => new
            {
                id = pc.ProgramCourseId,
                code = pc.Course?.CourseCode,
                title = pc.Course?.Description,
                lec = pc.Course?.LecUnits ?? 0,
                lab = pc.Course?.LabUnits ?? 0,
                units = (pc.Course?.LecUnits ?? 0) + (pc.Course?.LabUnits ?? 0),
                prereq = pc.PrerequisiteCourse?.CourseCode ?? "—"
            });
            return Json(new { ok = true, items });
        }

        [HttpPost("{id:int}/active")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id, [FromForm] bool isActive)
        {
            var ok = await _svc.SetProgramActiveAsync(id, isActive);
            return Json(new { ok });
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id, [FromForm] bool force = false)
        {
            if (id <= 0)
                return BadRequest(new { ok = false, message = "Invalid program id." });

            var hasCourses = _svc.HasAnyCourses(id);
            if (hasCourses && !force)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return StatusCode(409, new { ok = false, reason = "has_courses", message = "Program still has courses. Remove them first or force delete." });

                TempData["Programs.Error"] = "Cannot delete: program still has courses. Remove them first.";
                return Redirect(Request.Headers["Referer"].ToString() ?? "/admin/programs");
            }

            var adminUserId = CurrentAdminUserId();
            _svc.DiscardProgram(id, adminUserId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, deleted = true, id });

            TempData["Programs.Ok"] = "Program deleted.";
            return Redirect(Request.Headers["Referer"].ToString() ?? "/admin/programs");
        }

        [HttpPost("{id:int}/update")]
        [ValidateAntiForgeryToken]
        public IActionResult Update(
            [FromRoute] int id,
            [FromForm] string ProgramCode,
            [FromForm] string ProgramName,
            [FromForm] bool IsActive)
        {
            try
            {
                var adminUserId = CurrentAdminUserId();
                var ok = _svc.UpdateProgram(id, ProgramCode, ProgramName, IsActive, adminUserId);
                if (!ok) return NotFound(new { message = "Program not found." });
                return Ok(new { ok = true });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(409, new { message = ex.Message });
            }
            catch
            {
                return StatusCode(500, new { message = "Unexpected server error." });
            }
        }
    }
}
