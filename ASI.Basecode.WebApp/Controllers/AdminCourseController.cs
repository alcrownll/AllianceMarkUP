using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic; // added for List<Course>

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminCoursesController : Controller
    {
        private readonly ICourseService _service;

        public AdminCoursesController(ICourseService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var courses = await _service.GetAllAsync() ?? new List<Course>();
            return View("~/Views/Admin/AdminCourses.cshtml", courses);
        }

        // NEW: fetch a single course for Edit modal prefill
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            // Assumes your service exposes GetByIdAsync(int)
            var c = await _service.GetByIdAsync(id);
            if (c == null) return NotFound();

            // Return only the fields the modal needs
            return Json(new
            {
                id = c.CourseId,
                code = c.CourseCode,
                description = c.Description,
                lecUnits = c.LecUnits,
                labUnits = c.LabUnits,
                totalUnits = c.TotalUnits,   // if computed server-side
            });
        }

        // Create: accept only the fields you expect (TotalUnits is computed server-side)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml",
                    await _service.GetAllAsync() ?? new List<Course>());

            await _service.CreateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        // Edit: include the key and the editable fields
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("CourseId,CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml",
                    await _service.GetAllAsync() ?? new List<Course>());

            await _service.UpdateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
