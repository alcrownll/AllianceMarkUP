using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly ITeacherCourseService _teacherCourseService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly ITeacherDashboardService _dashboardService;

        public TeacherController(
            IProfileService profileService,
            ITeacherCourseService teacherCourseService,
            IHttpContextAccessor httpContext,
            ITeacherDashboardService dashboardService)
        {
            _profileService = profileService;
            _teacherCourseService = teacherCourseService;
            _httpContext = httpContext;
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewData["PageHeader"] = "Teacher Dashboard";

            // Get IdNumber from session (the service needs IdNumber)
            var idNumber = GetCurrentIdNumber();
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("TeacherLogin", "Login");

            // Build dynamic VM
            var vm = await _dashboardService.BuildAsync(idNumber);

            return View("TeacherDashboard", vm);
        }

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

        public async Task<IActionResult> AssignedCourses(string semester = null, string program = null,
            int? yearLevel = null, string searchFirstName = null, string searchLastName = null, string searchId = null, string searchRemarks = null, 
            string sortBy = "lastName", string sortOrder = "asc")
        {
            ViewData["PageHeader"] = "Assigned Courses";

            try
            {
                var userId = _profileService.GetCurrentUserId();
                var teacherId = GetCurrentTeacherId();

                if (teacherId == 0)
                {
                    TempData["Error"] = $"Unable to identify teacher. UserId: {userId}, TeacherId: {teacherId}. Please check if you have a Teacher record.";
                    ViewBag.CurrentSemester = _teacherCourseService.GetCurrentSemesterName();
                    return View("TeacherCourses", new List<TeacherClassScheduleViewModel>());
                }

                var classSchedules = await _teacherCourseService.GetTeacherClassSchedulesAsync(teacherId, semester);

                if (!string.IsNullOrEmpty(searchFirstName) || !string.IsNullOrEmpty(searchLastName) || !string.IsNullOrEmpty(searchId) ||
                    !string.IsNullOrEmpty(program) || yearLevel.HasValue || !string.IsNullOrEmpty(searchRemarks))
                {
                    if (!string.IsNullOrEmpty(program))
                    {
                        classSchedules = classSchedules.Where(c => c.Course == program).ToList();
                    }
                }

                var filteredStudents = await _teacherCourseService.SearchStudentsAsync(teacherId, searchFirstName, searchLastName, searchId, program, yearLevel, searchRemarks, sortBy, sortOrder);
                var hasFilters = !string.IsNullOrEmpty(searchFirstName) || !string.IsNullOrEmpty(searchLastName) || !string.IsNullOrEmpty(searchId) ||
                                !string.IsNullOrEmpty(program) || yearLevel.HasValue || !string.IsNullOrEmpty(searchRemarks);

                var programs = await _teacherCourseService.GetTeacherProgramsAsync(teacherId);
                var yearLevels = await _teacherCourseService.GetTeacherYearLevelsAsync(teacherId);

                ViewBag.Programs = programs;
                ViewBag.YearLevels = yearLevels;
                ViewBag.SelectedSemester = semester;
                ViewBag.FilterProgram = program;
                ViewBag.FilterYearLevel = yearLevel;
                ViewBag.SearchFirstName = searchFirstName;
                ViewBag.SearchLastName = searchLastName;
                ViewBag.SearchId = searchId;
                ViewBag.SearchRemarks = searchRemarks;
                ViewBag.SortBy = sortBy;
                ViewBag.SortOrder = sortOrder;
                ViewBag.FilteredStudents = filteredStudents;
                ViewBag.HasFilters = hasFilters;
                ViewBag.CurrentSemester = _teacherCourseService.GetCurrentSemesterName();

                return View("TeacherCourses", classSchedules);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while loading courses: {ex.Message}";
                
                ViewBag.CurrentSemester = _teacherCourseService.GetCurrentSemesterName();
                return View("TeacherCourses", new List<TeacherClassScheduleViewModel>());
            }
        }

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

        [HttpPost]
        public async Task<IActionResult> UpdateGrades([FromBody] System.Collections.Generic.List<StudentGradeUpdateModel> grades)
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

        [HttpPost]
        public async Task<IActionResult> UploadExcelGrades(IFormFile excelFile, int assignedCourseId)
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return Json(new { success = false, message = "Teacher not found" });

                if (excelFile == null || excelFile.Length == 0)
                    return Json(new { success = false, message = "No file uploaded" });

                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(excelFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return Json(new { success = false, message = "Only Excel files (.xlsx, .xls) are allowed" });

                if (excelFile.Length > 5 * 1024 * 1024)
                    return Json(new { success = false, message = "File size must be less than 5MB" });

                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                var parseResult = _teacherCourseService.ParseExcelFile(fileBytes);
                if (!parseResult.Success)
                {
                    return Json(new
                    {
                        success = false,
                        message = parseResult.Message,
                        errors = parseResult.Errors
                    });
                }

                var uploadResult = await _teacherCourseService.ProcessExcelGradeUploadAsync(assignedCourseId, parseResult.ProcessedGrades);

                return Json(new
                {
                    success = uploadResult.Success,
                    message = uploadResult.Message,
                    processedCount = uploadResult.ProcessedCount,
                    errorCount = uploadResult.ErrorCount,
                    errors = uploadResult.Errors
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred while processing the Excel file: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadGradeTemplate(int assignedCourseId)
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return BadRequest("Teacher not found");

                var students = await _teacherCourseService.GetStudentGradesForClassAsync(assignedCourseId);
                var courses = await _teacherCourseService.GetTeacherClassSchedulesAsync(teacherId);
                var course = courses.FirstOrDefault(c => c.AssignedCourseId == assignedCourseId);
                var teacherProfile = await _profileService.GetTeacherProfileAsync(_profileService.GetCurrentUserId());

                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Grades Template");

                worksheet.Cell(1, 1).Value = "ID Number";
                worksheet.Cell(1, 2).Value = "Last Name";
                worksheet.Cell(1, 3).Value = "First Name";
                worksheet.Cell(1, 4).Value = "Prelims";
                worksheet.Cell(1, 5).Value = "Midterm";
                worksheet.Cell(1, 6).Value = "Semi-Final";
                worksheet.Cell(1, 7).Value = "Final";

                var headerRange = worksheet.Range(1, 1, 1, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;

                for (int i = 0; i < students.Count; i++)
                {
                    var student = students[i];
                    var row = i + 2;

                    worksheet.Cell(row, 1).Value = student.IdNumber;
                    worksheet.Cell(row, 2).Value = student.LastName;
                    worksheet.Cell(row, 3).Value = student.FirstName;
                    worksheet.Cell(row, 4).Value = student.Prelims?.ToString() ?? "";
                    worksheet.Cell(row, 5).Value = student.Midterm?.ToString() ?? "";
                    worksheet.Cell(row, 6).Value = student.SemiFinal?.ToString() ?? "";
                    worksheet.Cell(row, 7).Value = student.Final?.ToString() ?? "";
                }

                worksheet.Columns().AdjustToContents();

                for (int col = 4; col <= 7; col++)
                {
                    var gradeRange = worksheet.Range(2, col, students.Count + 1, col);
                    gradeRange.CreateDataValidation().Decimal.Between(1.0, 5.0);
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var teacherName = $"{teacherProfile?.FirstName}_{teacherProfile?.LastName}".Replace(" ", "_");
                var edpCode = course?.EDPCode?.Replace(" ", "_") ?? "Unknown";
                var subject = course?.Subject?.Replace(" ", "_") ?? "Subject";
                var fileName = $"{teacherName}_{edpCode}_{subject}.xlsx";

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error generating template: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchStudents(string searchFirstName = null, string searchLastName = null, string searchId = null,
            string program = null, int? yearLevel = null)
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return Json(new { success = false, message = "Teacher not found" });

                var students = await _teacherCourseService.SearchStudentsAsync(teacherId, searchFirstName, searchLastName, searchId, program, yearLevel);
                return Json(new { success = true, students = students });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error searching students" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintGrades(int assignedCourseId, string edpCode = "", string subject = "", string schedule = "")
        {
            try
            {
                var teacherId = GetCurrentTeacherId();
                if (teacherId == 0)
                    return BadRequest("Teacher not found");

                // Get the course details and students with grades
                var students = await _teacherCourseService.GetStudentGradesForClassAsync(assignedCourseId);
                var courses = await _teacherCourseService.GetTeacherClassSchedulesAsync(teacherId);
                var course = courses.FirstOrDefault(c => c.AssignedCourseId == assignedCourseId);

                if (course == null)
                    return NotFound("Course not found");

                // Prepare ViewBag data for the print view
                ViewBag.CurrentSchoolYear = await _teacherCourseService.GetCurrentSchoolYearAsync();
                ViewBag.CurrentSemester = _teacherCourseService.GetCurrentSemesterName();
                ViewBag.EDPCode = course.EDPCode;
                ViewBag.Subject = course.Subject;
                ViewBag.Schedule = course.DateTime;
                ViewBag.Students = students;
                ViewBag.TotalStudents = students.Count;

                return View("Partials/_TeacherCoursesPrintGrades", students);
            }
            catch (Exception)
            {
                return BadRequest("Error loading grades for printing");
            }
        }

        public IActionResult Calendar()
        {
            return RedirectToAction("Index", "Calendar");
        }

        public IActionResult Notifications() => RedirectToAction("Index", "Notifications");

        // Logout
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("ASI_Basecode");
            return RedirectToAction("TeacherLogin", "Login");
        }

        #region Private Helper Methods

        private int GetCurrentTeacherId() => _profileService.GetCurrentTeacherId();

        private string GetCurrentIdNumber()
        {
           
            var id = HttpContext?.Session?.GetString("IdNumber");
            if (!string.IsNullOrWhiteSpace(id)) return id;

            
            var claim = User?.Claims?.FirstOrDefault(c => c.Type == "IdNumber")?.Value;
            return claim ?? string.Empty;
        }

        #endregion
    }
}
