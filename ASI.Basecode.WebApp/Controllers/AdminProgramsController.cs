using ASI.Basecode.Data.Models;            // Program, ProgramCourse
// ✅ add the repo interface namespace (adjust if yours differs)
// e.g., IProgramRepository namespace
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;  // if other endpoints need service models
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
        public AdminProgramsController(ICurriculumService svc) => _svc = svc;

        // GET /admin/programs/list
        [HttpGet("list")]
        public IActionResult List([FromQuery] string q = null)
        {
            var items = _svc.ListPrograms(q); // IEnumerable<Program>
            return PartialView("~/Views/Admin/Partials/_ProgramsTable.cshtml", items);
        }

        // POST /admin/programs/create
        [HttpPost("create")]
        public IActionResult Create([FromForm] string code, [FromForm] string name, [FromForm] string notes)
        {
            var p = _svc.CreateProgram(code, name, notes);
            return Json(new { ok = true, programId = p.ProgramId, code = p.ProgramCode, name = p.ProgramName });
        }

        // POST /admin/programs/add-course
        [HttpPost("add-course")]
        public IActionResult AddCourse([FromForm] int programId, [FromForm] int year, [FromForm] int term,
                                       [FromForm] int courseId, [FromForm] int prereqCourseId)
        {
            _svc.AddCourseToTerm(programId, year, term, courseId, prereqCourseId);
            return Json(new { ok = true });
        }

        // GET /admin/programs/{programId}/term?year=1&term=2
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

        // (legacy) POST /admin/programs/remove-course
        [HttpPost("remove-course")]
        public IActionResult RemoveCourse([FromForm] int programCourseId)
        {
            _svc.RemoveProgramCourse(programCourseId);
            return Json(new { ok = true });
        }

        // POST /admin/programs/{programId}/term/item/{programCourseId}/remove
        [ValidateAntiForgeryToken]
        [HttpPost("{programId:int}/term/item/{programCourseId:int}/remove")]
        public IActionResult RemoveTermItem(int programId, int programCourseId)
        {
            _svc.RemoveProgramCourse(programCourseId);
            return Json(new { ok = true });
        }

        // POST /admin/programs/cancel
        [HttpPost("cancel")]
        public IActionResult Cancel(
            [FromForm] int programId,
            [FromServices] ICurriculumService svc,
            [FromForm] bool force = false)
        {
            if (programId <= 0) return BadRequest(new { ok = false, message = "Invalid programId." });

            var hasCourses = svc.HasAnyCourses(programId);
            if (hasCourses && !force)
                return Json(new { ok = true, hasCourses = true });

            svc.DiscardProgram(programId);
            return Json(new { ok = true, deleted = true });
        }

        // POST /admin/programs/{programId}/term/bulk-add
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

    }
}
