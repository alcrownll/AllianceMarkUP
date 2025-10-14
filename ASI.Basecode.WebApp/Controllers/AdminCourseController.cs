using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin/courses")] // 🔹 add a route prefix so URLs are /admin/courses/...
    public class AdminCoursesController : Controller
    {
        private readonly ICourseService _service;
        public AdminCoursesController(ICourseService service) => _service = service;

        [HttpGet("")] // GET /admin/courses
        public async Task<IActionResult> Index()
        {
            var courses = await _service.GetAllAsync() ?? new List<Course>();
            return View("~/Views/Admin/AdminCourses.cshtml", courses);
        }

        // 🔹 JSON for the course picker: GET /admin/courses/all
        [HttpGet("all")]
        public async Task<IActionResult> All()
        {
            var items = (await _service.GetAllAsync() ?? new List<Course>())
                .Select(c => new {
                    courseId = c.CourseId,
                    courseCode = c.CourseCode,
                    description = c.Description,
                    lecUnits = c.LecUnits,
                    labUnits = c.LabUnits
                });
            return Json(new { items });
        }

        // GET /admin/courses/get?id=123  (kept as-is, or you can change to [HttpGet("{id:int}")])
        [HttpGet("get")]
        public async Task<IActionResult> Get(int id)
        {
            var c = await _service.GetByIdAsync(id);
            if (c == null) return NotFound();
            return Json(new
            {
                id = c.CourseId,
                code = c.CourseCode,
                description = c.Description,
                lecUnits = c.LecUnits,
                labUnits = c.LabUnits,
                totalUnits = c.TotalUnits,
            });
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml",
                    await _service.GetAllAsync() ?? new List<Course>());

            await _service.CreateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("CourseId,CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml",
                    await _service.GetAllAsync() ?? new List<Course>());

            await _service.UpdateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
