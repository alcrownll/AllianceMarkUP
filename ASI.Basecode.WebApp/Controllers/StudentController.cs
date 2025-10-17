using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IClassScheduleRepository _classScheduleRepository;
        private readonly IWebHostEnvironment _env; // for serving prospectus pdfs
        private readonly IProfileService _profileService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly INotificationService _notificationService;
        private readonly IStudentDashboardService _studentDashboardService;
        private readonly IStudyLoadService _studyLoadService;

        public StudentController(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            IUserRepository userRepository,
            IClassScheduleRepository classScheduleRepository,
            IWebHostEnvironment env,
            IProfileService profileService,
            IHttpContextAccessor httpContext,
            INotificationService notificationService,
            IStudentDashboardService studentDashboardService,
            IStudyLoadService studyLoadService)
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _userRepository = userRepository;
            _classScheduleRepository = classScheduleRepository;
            _env = env;
            _profileService = profileService;
            _httpContext = httpContext;
            _notificationService = notificationService;
            _studentDashboardService = studentDashboardService;
            _studyLoadService = studyLoadService;
        }

        // --------------------------------------------------------------------
        // DASHBOARD
        // --------------------------------------------------------------------
        public async Task<IActionResult> Dashboard()
        {
            ViewData["PageHeader"] = "Dashboard";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var vm = await _studentDashboardService.BuildAsync(idNumber);
            return View("StudentDashboard", vm);
        }

        // --------------------------------------------------------------------
        // PROFILE
        // --------------------------------------------------------------------
        public async Task<IActionResult> Profile()
        {
            ViewData["PageHeader"] = "Profile";

            int userId = _profileService.GetCurrentUserId();
            var vm = await _profileService.GetStudentProfileAsync(userId);
            if (vm == null) return NotFound();

            return View("StudentProfile", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveProfile(StudentProfileViewModel vm)
        {
            if (!ModelState.IsValid)
                return View("StudentProfile", vm);

            int userId = _profileService.GetCurrentUserId();
            await _profileService.UpdateStudentProfileAsync(userId, vm);
            TempData["ProfileSaved"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        // --------------------------------------------------------------------
        // STUDY LOAD
        // --------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> StudyLoad(string term = null)
        {
            ViewData["PageHeader"] = "Study Load";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            if (user == null)
                return View(new StudyLoadViewModel());

            // Build VM from service: terms are sourced from AssignedCourses via Grades
            var vm = await _studyLoadService.GetStudyLoadAsync(user.UserId, term);
            return View(vm); // View name is StudyLoad.cshtml
        }

        // --------------------------------------------------------------------
        // GRADES
        // --------------------------------------------------------------------
        public async Task<IActionResult> Grades(string schoolYear = null, string semester = null)
        {
            ViewData["PageHeader"] = "Grades";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            var defaultSchoolYear = GetCurrentSchoolYear();
            var viewModel = new StudentGradesViewModel
            {
                StudentName = user != null ? $"{user.FirstName} {user.LastName}" : "Student",
                SchoolYear = defaultSchoolYear
            };

            if (user == null)
                return View(viewModel);

            var student = await _studentRepository.GetStudents()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == user.UserId);

            if (student == null)
                return View(viewModel);

            viewModel.Program = student.Program;
            viewModel.Department = student.Department;
            viewModel.YearLevel = FormatYearLevel(student.YearLevel);

            var grades = await _gradeRepository.GetGrades()
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.AssignedCourse).ThenInclude(ac => ac.Course)
                .Include(g => g.AssignedCourse).ThenInclude(ac => ac.Teacher).ThenInclude(t => t.User)
                .AsNoTracking()
                .ToListAsync();

            var gradeRows = grades.Select(g =>
            {
                var assignedCourse = g.AssignedCourse;
                var course = assignedCourse?.Course;
                var teacherUser = assignedCourse?.Teacher?.User;
                var rowSemester = assignedCourse?.Semester ?? "N/A"; // e.g., "1st Semester"
                var rowSchoolYear = assignedCourse?.SchoolYear ?? defaultSchoolYear; // e.g., "2025-2026"
                var remarks = CalculateRemarksFromGrades(g.Prelims, g.Midterm, g.SemiFinal, g.Final);
                return new StudentGradeRowViewModel
                {
                    SubjectCode = assignedCourse?.EDPCode,
                    Description = course?.Description,
                    Instructor = teacherUser != null ? $"{teacherUser.FirstName} {teacherUser.LastName}" : "N/A",
                    Type = assignedCourse?.Type,
                    Units = course?.TotalUnits ?? assignedCourse?.Units ?? 0,
                    Prelims = g.Prelims,
                    Midterm = g.Midterm,
                    SemiFinal = g.SemiFinal,
                    Final = g.Final,
                    Remarks = remarks,
                    Semester = rowSemester,
                    SchoolYear = rowSchoolYear
                };
            }).ToList();

            if (!gradeRows.Any())
                return View(viewModel);

            var availableSchoolYears = gradeRows
                .Select(r => r.SchoolYear)
                .Where(sy => !string.IsNullOrWhiteSpace(sy))
                .Distinct()
                .OrderBy(sy => sy)
                .ToList();

            if (!availableSchoolYears.Any())
                availableSchoolYears.Add(defaultSchoolYear);

            var selectedSchoolYear = availableSchoolYears
                .FirstOrDefault(sy => string.Equals(sy, schoolYear, StringComparison.OrdinalIgnoreCase))
                ?? availableSchoolYears.FirstOrDefault();

            var availableSemesters = gradeRows
                .Where(r => string.Equals(r.SchoolYear, selectedSchoolYear, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Semester)
                .Where(se => !string.IsNullOrWhiteSpace(se))
                .Distinct()
                .OrderBy(se => se)
                .ToList();

            if (!availableSemesters.Any())
            {
                availableSemesters = gradeRows
                    .Select(r => r.Semester)
                    .Where(se => !string.IsNullOrWhiteSpace(se))
                    .Distinct()
                    .OrderBy(se => se)
                    .ToList();
            }

            var selectedSemester = availableSemesters
                .FirstOrDefault(se => string.Equals(se, semester, StringComparison.OrdinalIgnoreCase))
                ?? availableSemesters.FirstOrDefault();

            var filteredGrades = gradeRows
                .Where(r =>
                    (string.IsNullOrWhiteSpace(selectedSchoolYear) || string.Equals(r.SchoolYear, selectedSchoolYear, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(selectedSemester) || string.Equals(r.Semester, selectedSemester, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var finals = filteredGrades
                .Where(r => r.Final.HasValue)
                .Select(r => r.Final.Value)
                .ToList();

            viewModel.AvailableSchoolYears = availableSchoolYears;
            viewModel.AvailableSemesters = availableSemesters;
            viewModel.SelectedSchoolYear = selectedSchoolYear;
            viewModel.SelectedSemester = selectedSemester;
            viewModel.SchoolYear = selectedSchoolYear ?? defaultSchoolYear;
            viewModel.Semester = selectedSemester ?? viewModel.Semester;
            viewModel.Grades = filteredGrades;
            viewModel.Gpa = finals.Any() ? decimal.Round(finals.Average(), 2) : (decimal?)null;

            return View(viewModel);
        }

        // --------------------------------------------------------------------
        // CALENDAR & NOTIFICATIONS
        // --------------------------------------------------------------------
        public IActionResult Calendar()
        {
            ViewData["PageHeader"] = "Calendar";
            return View("~/Views/Shared/Partials/Calendar.cshtml");
        }

        public IActionResult Notifications() => RedirectToAction("Index", "Notifications");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkNotificationRead(int id)
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkRead(userId, id);
            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllNotificationsRead()
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkAllRead(userId);
            return RedirectToAction(nameof(Notifications));
        }

        // --------------------------------------------------------------------
        // PROSPECTUS DOWNLOAD (BSCS / BSIT)
        // --------------------------------------------------------------------
        [HttpGet("/Student/DownloadProspectus")]
        public async Task<IActionResult> DownloadProspectus([FromQuery] string program = null)
        {
            // If a program is not provided (normal case), resolve from logged-in student
            if (string.IsNullOrWhiteSpace(program))
            {
                var idNumber = HttpContext.Session.GetString("IdNumber");
                if (string.IsNullOrEmpty(idNumber))
                    return RedirectToAction("StudentLogin", "Account");

                var user = await _userRepository.GetUsers()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.IdNumber == idNumber);
                if (user == null) return NotFound("User not found.");

                var student = await _studentRepository.GetStudents()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == user.UserId);
                if (student == null) return NotFound("Student profile not found.");

                program = student.Program;
            }

            var prog = (program ?? string.Empty).Trim().ToUpperInvariant();

            // Accept strict codes or common variants
            string fileName = prog switch
            {
                "BSCS" => "BSCS.pdf",
                "BSIT" => "BSIT.pdf",
                _ when prog.Contains("IT") => "BSIT.pdf",
                _ when prog.Contains("CS") => "BSCS.pdf",
                _ => null
            };

            if (fileName is null)
                return NotFound("Program not mapped to a prospectus (expected BSCS or BSIT).");

            // Files expected at: wwwroot/prospectus/BSCS.pdf and BSIT.pdf
            var path = Path.Combine(_env.WebRootPath, "prospectus", fileName);
            if (!System.IO.File.Exists(path))
                return NotFound($"Prospectus file not found: {fileName}");

            // Force a download (use inline by omitting the downloadName)
            return PhysicalFile(path, "application/pdf", fileName);
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------
        private static string GetCurrentSchoolYear()
        {
            var now = DateTime.Now;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        private static string FormatYearLevel(string yearLevel)
        {
            if (string.IsNullOrWhiteSpace(yearLevel)) return "N/A";
            if (!int.TryParse(yearLevel, out var level)) return yearLevel;

            var suffix = "th";
            if (level % 100 is < 11 or > 13)
            {
                suffix = (level % 10) switch
                {
                    1 => "st",
                    2 => "nd",
                    3 => "rd",
                    _ => "th"
                };
            }
            return $"{level}{suffix} Year";
        }

        private static string AbbrevDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "TH",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "SAT",
            DayOfWeek.Sunday => "SUN",
            _ => ""
        };

        private static string To12h(TimeSpan t)
        {
            var dt = DateTime.Today.Add(t);
            return dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        // Calculate remarks based on grades (same logic as TeacherCourseService)
        private static string CalculateRemarksFromGrades(decimal? prelims, decimal? midterm, decimal? semiFinal, decimal? final)
        {
            var components = new (decimal? Score, decimal Weight)[]
            {
                (prelims,   0.3m),
                (midterm,   0.3m),
                (semiFinal, 0.2m),
                (final,     0.2m)
            };

            var weightedTotal = 0m;
            var weightSum = 0m;

            foreach (var c in components)
            {
                if (c.Score.HasValue)
                {
                    weightedTotal += c.Score.Value * c.Weight;
                    weightSum += c.Weight;
                }
            }

            if (weightSum <= 0) return "INCOMPLETE";

            var gpa = Math.Round(weightedTotal / weightSum, 2);
            return gpa <= 3.0m ? "PASSED" : "FAILED";
        }

        // Example calendar split: Jun–Oct => 1st, Nov–Mar => 2nd, else Mid
        private static string GetCurrentSemesterName()
        {
            var now = DateTime.Now;
            if (now.Month is >= 6 and <= 10) return "1st";
            if (now.Month is >= 11 || now.Month <= 3) return "2nd";
            return "Mid";
        }

        // Match current term based on text in AssignedCourse.Semester
        private static bool MatchesCurrentTerm(string assignedCourseSemester, string currentSy, string currentSemName)
        {
            if (string.IsNullOrWhiteSpace(assignedCourseSemester)) return false;

            var syMatch = Regex.IsMatch(assignedCourseSemester, Regex.Escape(currentSy));
            var semMatch = assignedCourseSemester.Contains(currentSemName, StringComparison.OrdinalIgnoreCase);

            // Be forgiving: if either matches, count it.
            return syMatch || semMatch;
        }
    }
}
