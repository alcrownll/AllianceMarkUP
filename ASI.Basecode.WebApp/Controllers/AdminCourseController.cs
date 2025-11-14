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
    [Route("admin/courses")]
    public class AdminCoursesController : Controller
    {
        private readonly ICourseService _service;

        public AdminCoursesController(ICourseService service) => _service = service;

        // Helper method to detect AJAX requests
        private bool IsAjaxRequest() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            || Request.Headers["Accept"].ToString().Contains("application/json");

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var courses = await _service.GetAllAsync() ?? Enumerable.Empty<Course>();
            return View("~/Views/Admin/AdminCourses.cshtml", courses);
        }

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
            // Always return JSON for this endpoint
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Validation failed.";

                // 400 with JSON body
                return BadRequest(new { ok = false, message = firstError });
            }

            try
            {
                await _service.CreateAsync(course);

                // 200 with JSON body
                return Ok(new
                {
                    ok = true,
                    courseId = course.CourseId,
                    code = course.CourseCode,
                    description = course.Description
                });
            }
            catch (InvalidOperationException ex)
            {
                // e.g. duplicate course code, etc.
                return Conflict(new { ok = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { ok = false, message = "An error occurred while creating the course." });
            }
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("CourseId,CourseCode,Description,LecUnits,LabUnits")] Course course)
        {
            // Always return JSON for this endpoint
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Validation failed.";

                return BadRequest(new { ok = false, message = firstError });
            }

            try
            {
                await _service.UpdateAsync(course);

                return Ok(new { ok = true });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { ok = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { ok = false, message = "An error occurred while updating the course." });
            }
        }


        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return Json(new { success = true, message = "Course deleted successfully." });
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Error message: {ex.InnerException.Message}");
                }

                if (ex.InnerException?.Message.Contains("ProgramCourses_CourseId_fkey") == true)
                {
                    return Json(new { success = false, message = "This course cannot be deleted because it is referenced in ProgramCourses or AssignedCourses." });
                }
                else
                {
                    return Json(new { success = false, message = "An error occurred while deleting the course. Please try again." });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while deleting the course. Please try again." });
            }
        }

        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var courses = await _service.GetAllAsync() ?? Enumerable.Empty<Course>();
            // Return just the table markup
            return PartialView("~/Views/Admin/Partials/_CoursesTable.cshtml", courses.ToList());
        }

    }
}