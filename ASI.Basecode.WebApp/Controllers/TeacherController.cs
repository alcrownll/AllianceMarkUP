using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace ASI.Basecode.WebApp.Controllers
{
    public class TeacherController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ITeacherCourseService _teacherCourseService;
        private readonly IHttpContextAccessor _httpContext;

        public TeacherController(IProfileService profileService,
            ITeacherCourseService teacherCourseService,
            IHttpContextAccessor httpContext)
        {
            _profileService = profileService;
            _teacherCourseService = teacherCourseService;
            _httpContext = httpContext;
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult Dashboard()
        {
            ViewData["PageHeader"] = "Teacher Dashboard";
            return View("TeacherDashboard");
        }

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Profile()
        {
            ViewData["PageHeader"] = "Profile";

            int userId = _profileService.GetCurrentUserId();
            var vm = await _profileService.GetTeacherProfileAsync(userId);
            if (vm == null) return NotFound();
            return View("TeacherProfile", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> SaveProfile(TeacherProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("TeacherProfile", vm);
            }

            int userId = _profileService.GetCurrentUserId();
            await _profileService.UpdateTeacherProfileAsync(userId, vm);
            TempData["ProfileSaved"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> AssignedCourses(string semester = null, string program = null, 
            int? yearLevel = null, string searchName = null, string searchId = null)
        {
            ViewData["PageHeader"] = "Assigned Courses";
            
            try
            {
                var userId = _profileService.GetCurrentUserId();
                var teacherId = GetCurrentTeacherId();
                
                // Debug information
                ViewBag.DebugUserId = userId;
                ViewBag.DebugTeacherId = teacherId;
                
                if (teacherId == 0)
                {
                    TempData["Error"] = $"Unable to identify teacher. UserId: {userId}, TeacherId: {teacherId}. Please check if you have a Teacher record.";
                    return View("TeacherCourses", new List<TeacherClassScheduleViewModel>());
                }

                // Get teacher's assigned courses for grade entry tab
                var classSchedules = await _teacherCourseService.GetTeacherClassSchedulesAsync(teacherId, semester);
                
                // Apply client-side filtering if search parameters are provided
                if (!string.IsNullOrEmpty(searchName) || !string.IsNullOrEmpty(searchId) || 
                    !string.IsNullOrEmpty(program) || yearLevel.HasValue)
                {
                    // Filter courses based on search criteria - this is a simplified approach
                    // In a more complex scenario, you might want to filter at the service level
                    if (!string.IsNullOrEmpty(program))
                    {
                        classSchedules = classSchedules.Where(c => c.Course == program).ToList();
                    }
                }
                
                // Get filtered students for Grade View tab
                // Always call SearchStudentsAsync - it will return all students when no filters are applied
                var filteredStudents = await _teacherCourseService.SearchStudentsAsync(teacherId, searchName, searchId, program, yearLevel);
                var hasFilters = !string.IsNullOrEmpty(searchName) || !string.IsNullOrEmpty(searchId) || 
                                !string.IsNullOrEmpty(program) || yearLevel.HasValue;
                
                // Get filter options
                var programs = await _teacherCourseService.GetTeacherProgramsAsync(teacherId);
                var yearLevels = await _teacherCourseService.GetTeacherYearLevelsAsync(teacherId);

                ViewBag.Programs = programs;
                ViewBag.YearLevels = yearLevels;
                ViewBag.SelectedSemester = semester;
                ViewBag.FilterProgram = program;
                ViewBag.FilterYearLevel = yearLevel;
                ViewBag.SearchName = searchName;
                ViewBag.SearchId = searchId;
                ViewBag.FilteredStudents = filteredStudents;
                ViewBag.HasFilters = hasFilters;
                ViewBag.DebugCoursesCount = classSchedules?.Count ?? 0;
                ViewBag.CurrentSchoolYear = GetCurrentSchoolYear();

                return View("TeacherCourses", classSchedules);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while loading courses: {ex.Message}";
                return View("TeacherCourses", new List<TeacherClassScheduleViewModel>());
            }
        }

        [Authorize(Roles = "Teacher")]
        [HttpGet]
        public async Task<IActionResult> GetStudentsForCourse(int assignedCourseId, string examType = "Prelim")
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return Json(new { success = false, message = "Teacher not found" });

                var students = await _teacherCourseService.GetStudentGradesForClassAsync(assignedCourseId, examType);
                return Json(new { success = true, students = students });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error loading students" });
            }
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost]
        public async Task<IActionResult> UpdateGrades([FromBody] List<StudentGradeUpdateModel> grades)
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return Json(new { success = false, message = "Teacher not found" });

                var success = await _teacherCourseService.UpdateStudentGradesAsync(grades);
                
                if (success)
                    return Json(new { success = true, message = "Grades updated successfully" });
                else
                    return Json(new { success = false, message = "Failed to update grades" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while updating grades" });
            }
        }

        [Authorize(Roles = "Teacher")]
        [HttpGet]
        public async Task<IActionResult> SearchStudents(string searchName = null, string searchId = null, 
            string program = null, int? yearLevel = null)
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return Json(new { success = false, message = "Teacher not found" });

                var students = await _teacherCourseService.SearchStudentsAsync(teacherId, searchName, searchId, program, yearLevel);
                return Json(new { success = true, students = students });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error searching students" });
            }
        }



        [Authorize(Roles = "Teacher")]
        public IActionResult Calendar()
        {
            return RedirectToAction("Index", "Calendar");
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult Notifications() => RedirectToAction("Index", "Notifications");

        //Logout

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("ASI_Basecode");
            return RedirectToAction("TeacherLogin", "Login"); // redirect to login page
        }

        // TEMPORARY SEED DATA METHOD - Remove after debugging
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> SeedTestData()
        {
            try
            {
                var userId = _profileService.GetCurrentUserId();
                
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ASI.Basecode.Data.AsiBasecodeDBContext>();
                    
                    // Check if teacher exists
                    var teacher = context.Teachers.FirstOrDefault(t => t.UserId == userId);
                    if (teacher == null)
                    {
                        // Create teacher record
                        teacher = new ASI.Basecode.Data.Models.Teacher
                        {
                            UserId = userId,
                            Position = "Faculty"
                        };
                        context.Teachers.Add(teacher);
                        await context.SaveChangesAsync();
                    }
                    
                    // Create sample course if none exists
                    if (!context.Courses.Any())
                    {
                        var course = new ASI.Basecode.Data.Models.Course
                        {
                            CourseCode = "CS-101",
                            Description = "Introduction to Computer Science",
                            LecUnits = 3,
                            LabUnits = 0,
                            TotalUnits = 3
                        };
                        context.Courses.Add(course);
                        await context.SaveChangesAsync();
                    }
                    
                    // Create assigned course if none exists
                    if (!context.AssignedCourses.Any(ac => ac.TeacherId == teacher.TeacherId))
                    {
                        var course = context.Courses.First();
                        var assignedCourse = new ASI.Basecode.Data.Models.AssignedCourse
                        {
                            EDPCode = "12345",
                            CourseId = course.CourseId,
                            Type = "LEC",
                            Units = 3,
                            Program = "BSCS",
                            TeacherId = teacher.TeacherId,
                            Semester = "2024-2025-1"
                        };
                        context.AssignedCourses.Add(assignedCourse);
                        await context.SaveChangesAsync();
                    }
                    
                    return Json(new { Success = true, Message = "Test data created successfully!" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { Success = false, Error = ex.Message });
            }
        }

        // TEMPORARY DEBUG METHOD - Remove after debugging
        [Authorize(Roles = "Teacher")]
        public IActionResult DebugData()
        {
            try
            {
                var userId = _profileService.GetCurrentUserId();
                var teacherId = GetCurrentTeacherId();
                
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ASI.Basecode.Data.AsiBasecodeDBContext>();
                    
                    var teacher = context.Teachers.FirstOrDefault(t => t.UserId == userId);
                    var assignedCourses = context.AssignedCourses.Where(ac => ac.TeacherId == teacherId).ToList();
                    var allTeachers = context.Teachers.ToList();
                    var allAssignedCourses = context.AssignedCourses.ToList();
                    
                    var debugInfo = new
                    {
                        UserId = userId,
                        TeacherId = teacherId,
                        TeacherExists = teacher != null,
                        TeacherRecord = teacher,
                        AssignedCoursesCount = assignedCourses.Count,
                        AssignedCourses = assignedCourses,
                        TotalTeachers = allTeachers.Count,
                        TotalAssignedCourses = allAssignedCourses.Count
                    };
                    
                    return Json(debugInfo);
                }
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        #region Private Helper Methods

        private int GetCurrentTeacherId()
        {
            try
            {
                // Get user ID from session or claims
                var userId = _profileService.GetCurrentUserId();
                if (userId <= 0)
                    return 0;

                // Need to query the Teacher table to get the actual TeacherId using the UserId
                // We'll use the database context through one of our existing services
                // This is a temporary solution - ideally this should be in a service
                using (var scope = HttpContext.RequestServices.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ASI.Basecode.Data.AsiBasecodeDBContext>();
                    var teacher = context.Teachers.FirstOrDefault(t => t.UserId == userId);
                    return teacher?.TeacherId ?? 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private static string GetCurrentSchoolYear()
        {
            var now = DateTime.Now;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        #endregion
    }
}
