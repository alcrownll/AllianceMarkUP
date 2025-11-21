using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Exceptions;
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
        private readonly IProfileService _profileService; //for notification

        public AdminCoursesController(
            ICourseService service,
            IProfileService profileService) //for notification
        {
            _service = service;
            _profileService = profileService; //for notification
        }

        private int CurrentAdminUserId() => _profileService.GetCurrentUserId(); //for notification

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
            try
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
            catch (NotFoundException ex)
            {
                return NotFound(new { ok = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { ok = false, message = "An error occurred while retrieving the course." });
            }
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
                var adminUserId = CurrentAdminUserId(); //for notification
                await _service.CreateAsync(course, adminUserId); //for notification

                // 200 with JSON body
                return Ok(new
                {
                    ok = true,
                    courseId = course.CourseId,
                    code = course.CourseCode,
                    description = course.Description
                });
            }
            catch (DuplicateCourseCodeException ex)
            {
                // Duplicate course code
                return Conflict(new { ok = false, message = ex.Message });
            }
            catch (ValidationException ex)
            {
                // Validation errors
                return BadRequest(new { ok = false, message = ex.Message });
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
                var adminUserId = CurrentAdminUserId(); //for notification
                await _service.UpdateAsync(course, adminUserId); //for notification

                return Ok(new { ok = true });
            }
            catch (DuplicateCourseCodeException ex)
            {
                // Duplicate course code
                return Conflict(new { ok = false, message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                // Course not found
                return NotFound(new { ok = false, message = ex.Message });
            }
            catch (ValidationException ex)
            {
                // Validation errors
                return BadRequest(new { ok = false, message = ex.Message });
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
                var adminUserId = CurrentAdminUserId(); //for notification
                await _service.DeleteAsync(id, adminUserId); //for notification

                if (IsAjaxRequest())
                    return Ok(new { success = true, message = "Course deleted successfully." });

                TempData["ToastSuccess"] = "Course deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (CourseInUseException ex)
            {
                if (IsAjaxRequest())
                    return Conflict(new { success = false, message = ex.Message });

                TempData["ToastError"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (NotFoundException ex)
            {
                if (IsAjaxRequest())
                    return NotFound(new { success = false, message = ex.Message });

                TempData["ToastError"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (BaseServiceException ex)
            {
                // any other custom service exception
                if (IsAjaxRequest())
                    return BadRequest(new { success = false, message = ex.Message });

                TempData["ToastError"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex}");

                if (IsAjaxRequest())
                    return StatusCode(500, new { success = false, message = "An unexpected error occurred while deleting the course." });

                TempData["ToastError"] = "An unexpected error occurred while deleting the course.";
                return RedirectToAction(nameof(Index));
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
