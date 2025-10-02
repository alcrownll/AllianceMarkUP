using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
            var courses = await _service.GetAllAsync();
            return View("~/Views/Admin/AdminCourses.cshtml", courses);
        }

        // Create: accept only the fields you expect (TotalUnits is computed server-side)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml", await _service.GetAllAsync());

            await _service.CreateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        // Edit: include the key and the editable fields
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("CourseId,CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml", await _service.GetAllAsync());

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
