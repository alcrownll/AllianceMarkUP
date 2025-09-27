using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class StudentController : Controller
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUserRepository _userRepository;

        public StudentController(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            IUserRepository userRepository)
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> StudentDashboard()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "Test Student"),
                new Claim(ClaimTypes.Role, "Student")
            };

            var identity = new ClaimsIdentity(claims, "ASI_Basecode");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("ASI_Basecode", principal);

            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Student")]
        public IActionResult Dashboard()
        {
            ViewData["PageHeader"] = "Dashboard";
            return View("StudentDashboard");
        }

        [Authorize(Roles = "Student")]
        public IActionResult Profile()
        {
            ViewData["PageHeader"] = "Profile";
            return View("~/Views/Shared/Partials/Profile.cshtml");
        }

        [Authorize(Roles = "Student")]
        public IActionResult StudyLoad()
        {
            ViewData["PageHeader"] = "Study Load";
            return View();
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Grades(string schoolYear = null, string semester = null)
        {
            ViewData["PageHeader"] = "Grades";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
            {
                return RedirectToAction("StudentLogin", "Account");
            }

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
            {
                return View(viewModel);
            }

            var student = await _studentRepository.GetStudents()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == user.UserId);

            if (student == null)
            {
                return View(viewModel);
            }

            viewModel.Program = student.Program;
            viewModel.Department = student.Department;
            viewModel.YearLevel = FormatYearLevel(student.YearLevel);

            var grades = await _gradeRepository.GetGrades()
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.AssignedCourse)
                    .ThenInclude(ac => ac.Course)
                .Include(g => g.AssignedCourse)
                    .ThenInclude(ac => ac.Teacher)
                        .ThenInclude(t => t.User)
                .AsNoTracking()
                .ToListAsync();

            var gradeRows = grades.Select(g =>
            {
                var assignedCourse = g.AssignedCourse;
                var course = assignedCourse?.Course;
                var teacherUser = assignedCourse?.Teacher?.User;
                var rowSemester = assignedCourse?.Semester ?? "N/A";
                var rowSchoolYear = ExtractSchoolYear(rowSemester) ?? defaultSchoolYear;
                var remarks = !string.IsNullOrWhiteSpace(g.Remarks)
                    ? g.Remarks
                    : g.Final.HasValue
                        ? (g.Final.Value >= 75 ? "PASSED" : "FAILED")
                        : "N/A";

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
            {
                return View(viewModel);
            }

            var availableSchoolYears = gradeRows
                .Select(r => r.SchoolYear)
                .Where(sy => !string.IsNullOrWhiteSpace(sy))
                .Distinct()
                .OrderBy(sy => sy)
                .ToList();

            if (!availableSchoolYears.Any())
            {
                availableSchoolYears.Add(defaultSchoolYear);
            }

            var selectedSchoolYear = availableSchoolYears
                .FirstOrDefault(sy => string.Equals(sy, schoolYear, System.StringComparison.OrdinalIgnoreCase))
                ?? availableSchoolYears.FirstOrDefault();

            var availableSemesters = gradeRows
                .Where(r => string.Equals(r.SchoolYear, selectedSchoolYear, System.StringComparison.OrdinalIgnoreCase))
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
                .FirstOrDefault(se => string.Equals(se, semester, System.StringComparison.OrdinalIgnoreCase))
                ?? availableSemesters.FirstOrDefault();

            var filteredGrades = gradeRows
                .Where(r =>
                    (string.IsNullOrWhiteSpace(selectedSchoolYear) || string.Equals(r.SchoolYear, selectedSchoolYear, System.StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(selectedSemester) || string.Equals(r.Semester, selectedSemester, System.StringComparison.OrdinalIgnoreCase)))
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

        private static string GetCurrentSchoolYear()
        {
            var now = System.DateTime.Now;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        private static string ExtractSchoolYear(string semester)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return null;
            }

            var match = Regex.Match(semester, @"(\d{4}).*?(\d{4})");
            if (match.Success && match.Groups.Count >= 3)
            {
                return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
            }

            return null;
        }

        private static string FormatYearLevel(string yearLevel)
        {
            if (string.IsNullOrWhiteSpace(yearLevel))
            {
                return "N/A";
            }

            if (!int.TryParse(yearLevel, out var level))
            {
                return yearLevel;
            }

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

        [Authorize(Roles = "Student")]
        public IActionResult Calendar()
        {
            ViewData["PageHeader"] = "Calendar";
            return View("~/Views/Shared/Partials/Calendar.cshtml");
        }

        [Authorize(Roles = "Student")]
        public IActionResult Notifications()
        {
            ViewData["PageHeader"] = "Student Notifications";
            return View("~/Views/Shared/Partials/Notifications.cshtml");
        }

    }
}
