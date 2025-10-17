using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels; // <— keep if other endpoints need service models; safe to remove if unused
using ASI.Basecode.Data.Models;           // Program, ProgramCourse
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    [ApiController]
    [Route("admin/programs")]
    public class AdminProgramsController : Controller
    {
        private readonly ICurriculumService _svc;
        public AdminProgramsController(ICurriculumService svc) => _svc = svc;

        // ========== READ-ONLY Programs table partial ==========
        // GET /admin/programs/list
        [HttpGet("list")]
        public IActionResult List([FromQuery] string q = null)
        {
            var items = _svc.ListPrograms(q); // IEnumerable<Program>
            return PartialView("~/Views/Admin/Partials/_ProgramsTable.cshtml", items);
        }

        // ========== Add Program (used by the modal) ==========
        // POST /admin/programs/create
        [HttpPost("create")]
        public IActionResult Create([FromForm] string code, [FromForm] string name, [FromForm] string notes)
        {
            var p = _svc.CreateProgram(code, name, notes); // notes can be ignored in your service if not in DB
            return Json(new { ok = true, programId = p.ProgramId, code = p.ProgramCode, name = p.ProgramName });
        }

        // ========== Manage ProgramCourses (step-by-step) ==========
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
                code = pc.Course?.CourseCode,
                title = pc.Course?.Description,
                lec = pc.Course?.LecUnits ?? 0,
                lab = pc.Course?.LabUnits ?? 0,
                units = (pc.Course?.LecUnits ?? 0) + (pc.Course?.LabUnits ?? 0),
                prereq = pc.PrerequisiteCourse?.CourseCode ?? "—"
            });
            return Json(new { ok = true, items });
        }

        // POST /admin/programs/remove-course
        [HttpPost("remove-course")]
        public IActionResult RemoveCourse([FromForm] int programCourseId)
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
    }
}
