using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.WebApp.Controllers
{
    public class StudentController : Controller
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IClassScheduleRepository _classScheduleRepository;
        private readonly IWebHostEnvironment _env; // for serving prospectus pdfs

        public StudentController(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            IUserRepository userRepository,
            IClassScheduleRepository classScheduleRepository,
            IWebHostEnvironment env)
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _userRepository = userRepository;
            _classScheduleRepository = classScheduleRepository;
            _env = env;
        }

        // --------------------------------------------------------------------
        // DASHBOARD
        // --------------------------------------------------------------------
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Dashboard()
        {
            ViewData["PageHeader"] = "Dashboard";

            // Identify logged-in student
            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            if (user == null)
                return View("StudentDashboard", new StudentDashboardViewModel());

            var student = await _studentRepository.GetStudents()
                .Include(s => s.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == user.UserId);

            if (student == null)
                return View("StudentDashboard", new StudentDashboardViewModel());

            var grades = await _gradeRepository.GetGrades()
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.AssignedCourse).ThenInclude(ac => ac.Course)
                .AsNoTracking()
                .ToListAsync();

            // ---- Cumulative GWA (weighted by units) ----
            decimal totalWeighted = 0m;
            decimal totalUnitsForWeight = 0m;

            foreach (var g in grades)
            {
                var units = (g.AssignedCourse?.Units)
                            ?? (g.AssignedCourse?.Course?.TotalUnits)
                            ?? 0;
                if (units <= 0) units = 1;

                if (!g.Final.HasValue) continue;
                var gwaValue = MapPercentToGwa((decimal)g.Final.Value);

                totalWeighted += gwaValue * units;
                totalUnitsForWeight += units;
            }

            decimal? cumulativeGwa = null;
            if (totalUnitsForWeight > 0)
                cumulativeGwa = Math.Round(totalWeighted / totalUnitsForWeight, 2);

            var currentSy = GetCurrentSchoolYear();            // e.g., "2025-2026"
            var currentSemNameShort = GetCurrentSemesterName(); // "1st"/"2nd"/"Mid"

            var currentTermUnits = grades
                .Where(g => g.AssignedCourse != null
                            && MatchesCurrentTerm(g.AssignedCourse.Semester, currentSy, currentSemNameShort))
                .Select(g =>
                    (g.AssignedCourse?.Units as int?)
                    ?? g.AssignedCourse?.Course?.TotalUnits
                    ?? 0
                )
                .Sum();

            bool isDeanList = cumulativeGwa.HasValue && cumulativeGwa.Value <= 1.75m;

            var vm = new StudentDashboardViewModel
            {
                StudentName = $"{student.User.FirstName} {student.User.LastName}",
                Program = student.Program,
                YearLevel = FormatYearLevel(student.YearLevel),
                CumulativeGwa = cumulativeGwa,
                CurrentTermUnits = currentTermUnits,
                IsDeanListEligible = isDeanList,
                CurrentSchoolYear = currentSy,
                CurrentSemesterName = currentSemNameShort + " Semester"
            };

            return View("StudentDashboard", vm);
        }

        // --------------------------------------------------------------------
        // PROFILE
        // --------------------------------------------------------------------
        [Authorize(Roles = "Student")]
        public IActionResult Profile()
        {
            ViewData["PageHeader"] = "Profile";
            return View("~/Views/Shared/Partials/Profile.cshtml");
        }

        // --------------------------------------------------------------------
        // STUDY LOAD
        // --------------------------------------------------------------------
        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> StudyLoad()
        {
            ViewData["PageHeader"] = "Study Load";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            if (user == null)
                return View(new StudentStudyLoadViewModel());

            var student = await _studentRepository.GetStudents()
                .Include(s => s.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == user.UserId);

            if (student == null)
                return View(new StudentStudyLoadViewModel());

            var grades = await _gradeRepository.GetGrades()
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.AssignedCourse).ThenInclude(ac => ac.Course)
                .Include(g => g.AssignedCourse).ThenInclude(ac => ac.Teacher).ThenInclude(t => t.User)
                .AsNoTracking()
                .ToListAsync();

            var assignedCourseIds = grades.Select(g => g.AssignedCourseId).Distinct().ToList();

            var schedules = await _classScheduleRepository.GetClassSchedules()
                .Where(cs => assignedCourseIds.Contains(cs.AssignedCourseId))
                .AsNoTracking()
                .ToListAsync();

            var schedLookup = schedules
                .GroupBy(s => s.AssignedCourseId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = grades.Select(g =>
            {
                var ac = g.AssignedCourse;
                var course = ac?.Course;
                var tchUser = ac?.Teacher?.User;

                schedLookup.TryGetValue(ac?.AssignedCourseId ?? 0, out var schedsForCourse);

                return new StudentStudyLoadRow
                {
                    EDPCode = ac?.EDPCode,
                    Subject = course?.CourseCode,
                    Description = course?.Description,
                    Instructor = tchUser != null ? $"{tchUser.FirstName} {tchUser.LastName}" : "N/A",
                    Units = ac?.Units > 0 ? ac.Units : ((course?.LecUnits ?? 0) + (course?.LabUnits ?? 0)),
                    Type = ac?.Type,
                    Room = schedsForCourse?.FirstOrDefault()?.Room ?? "",
                    DateTime = FormatSchedule_DayOfWeek_TimeSpan(schedsForCourse)
                };
            })
            .OrderBy(r => r.Subject)
            .ThenBy(r => r.Type)
            .ToList();

            var vm = new StudentStudyLoadViewModel
            {
                StudentName = $"{student.User.FirstName} {student.User.LastName}",
                Program = student.Program,
                YearLevel = FormatYearLevel(student.YearLevel),
                SelectedTerm = null,
                Terms = new List<TermItem>(),
                Rows = rows
            };

            return View(vm);
        }

        // --------------------------------------------------------------------
        // GRADES
        // --------------------------------------------------------------------
        [Authorize(Roles = "Student")]
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

        // --------------------------------------------------------------------
        // PROSPECTUS DOWNLOAD (BSCS / BSIT)
        // Explicit route to avoid 404s from conventional routing quirks.
        // --------------------------------------------------------------------
        [Authorize(Roles = "Student")]
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

        private static string ExtractSchoolYear(string semesterText)
        {
            if (string.IsNullOrWhiteSpace(semesterText)) return null;
            var m = Regex.Match(semesterText, @"(20\d{2})\D+(20\d{2})");
            return m.Success ? $"{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
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

        private static string FormatSchedule_DayOfWeek_TimeSpan(ICollection<ClassSchedule> scheds)
        {
            if (scheds == null || scheds.Count == 0) return "";

            var groups = scheds
                .OrderBy(s => s.Day)
                .ThenBy(s => s.StartTime)
                .GroupBy(s => new { s.StartTime, s.EndTime });

            var parts = new List<string>();
            foreach (var g in groups)
            {
                var dayStr = string.Join("", g.Select(x => AbbrevDay(x.Day)).Distinct());
                var timeStr = $"{To12h(g.Key.StartTime)} - {To12h(g.Key.EndTime)}";
                parts.Add($"{dayStr} ({timeStr})");
            }
            return string.Join("; ", parts);
        }

        // Map a 0–100 final grade to GWA (1.00–5.00). Adjust to your official scale.
        private static decimal MapPercentToGwa(decimal percent)
        {
            if (percent >= 96) return 1.00m;
            if (percent >= 93) return 1.25m;
            if (percent >= 90) return 1.50m;
            if (percent >= 87) return 1.75m;
            if (percent >= 84) return 2.00m;
            if (percent >= 81) return 2.25m;
            if (percent >= 78) return 2.50m;
            if (percent >= 75) return 2.75m;
            if (percent >= 70) return 3.00m;
            if (percent >= 65) return 4.00m;
            return 5.00m;
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
