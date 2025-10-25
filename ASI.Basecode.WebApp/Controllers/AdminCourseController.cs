using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin/courses")] // 🔹 add a route prefix so URLs are /admin/courses/...
    public class AdminCoursesController : Controller
    {
        private readonly ICourseService _service;

        // Primary constructor with simplified initialization
        public AdminCoursesController(ICourseService service) => _service = service;

        [HttpGet("")] // GET /admin/courses
        public async Task<IActionResult> Index()
        {
            var courses = await _service.GetAllAsync() ?? Enumerable.Empty<Course>();
            return View("~/Views/Admin/AdminCourses.cshtml", courses);
        }

        // 🔹 JSON for the course picker: GET /admin/courses/all
        [HttpGet("all")]
        public async Task<IActionResult> All()
        {
            var items = (await _service.GetAllAsync() ?? Enumerable.Empty<Course>())
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
                return View("~/Views/Admin/AdminCourses.cshtml", await _service.GetAllAsync() ?? Enumerable.Empty<Course>());

            await _service.CreateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("CourseId,CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Admin/AdminCourses.cshtml", await _service.GetAllAsync() ?? Enumerable.Empty<Course>());

            await _service.UpdateAsync(course);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Attempt to delete the course
                await _service.DeleteAsync(id);
                // If no error, pass a success message to be displayed
                return Json(new { success = true, message = "Course deleted successfully." });
            }
            catch (DbUpdateException ex)
            {
                // Debugging: Log or inspect the inner exception message
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Error message: {ex.InnerException.Message}");
                }

                // Check for foreign key violation
                if (ex.InnerException?.Message.Contains("ProgramCourses_CourseId_fkey") == true)
                {
                    // Foreign key violation error
                    return Json(new { success = false, message = "This course cannot be deleted because it is referenced in ProgramCourses or AssignedCourses." });
                }
                else
                {
                    // Handle other database exceptions
                    return Json(new { success = false, message = "An error occurred while deleting the course. Please try again." });
                }
            }
            catch (Exception)
            {
                // General exception handling (unexpected errors)
                return Json(new { success = false, message = "An error occurred while deleting the course. Please try again." });
            }
        }



    }
}
