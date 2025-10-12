using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class StudentDashboardService : IStudentDashboardService
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUserRepository _userRepository;

        public StudentDashboardService(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            IUserRepository userRepository)
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _userRepository = userRepository;
        }

        public async Task<StudentDashboardViewModel> BuildAsync(string idNumber)
        {
            var vmFallback = new StudentDashboardViewModel();

            if (string.IsNullOrWhiteSpace(idNumber))
                return vmFallback;

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            if (user == null)
                return vmFallback;

            var student = await _studentRepository.GetStudents()
                .Include(s => s.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == user.UserId);

            if (student == null)
                return vmFallback;

            var grades = await _gradeRepository.GetGrades()
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.AssignedCourse).ThenInclude(ac => ac.Course)
                .AsNoTracking()
                .ToListAsync();

            // ---------------- CUMULATIVE GWA ----------------
            decimal? cumulativeGwa = null;
            var finals = grades.Where(g => g.Final.HasValue).Select(g => g.Final!.Value).ToList();
            if (finals.Any())
                cumulativeGwa = Math.Round((decimal)finals.Average(), 2);

            // Dean’s List rule (same as your controller)
            var isDeanList = cumulativeGwa is >= 1.00m and <= 1.70m;

            // ---------------- CURRENT CONTEXT ----------------
            var currentSy = GetCurrentSchoolYear();
            var currentSemNameShort = GetCurrentSemesterName(); // "1st"/"2nd"/"Mid"

            var currentTermUnits = grades
                .Where(g => g.AssignedCourse != null
                            && MatchesCurrentTerm(g.AssignedCourse!.Semester, currentSy, currentSemNameShort))
                .Select(g =>
                    (g.AssignedCourse!.Units as int?) ??
                    g.AssignedCourse!.Course?.TotalUnits ?? 0
                )
                .Sum();

            // ---------------- SERIES (LATEST SY) ----------------
            var gradeRows = grades.Select(g =>
            {
                var semText = g.AssignedCourse?.Semester ?? string.Empty; // e.g., "1st Semester 2025-2026"
                var sy = ExtractSchoolYear(semText) ?? currentSy;
                var semShort = semText.Contains("1", StringComparison.OrdinalIgnoreCase) ? "1st"
                             : semText.Contains("2", StringComparison.OrdinalIgnoreCase) ? "2nd"
                             : currentSemNameShort;

                return new
                {
                    SchoolYear = sy,
                    SemShort = semShort,
                    g.Prelims,
                    g.Midterm,
                    g.SemiFinal,
                    g.Final
                };
            }).ToList();

            string latestSy = gradeRows
                .Select(r => r.SchoolYear)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OrderByDescending(s => s)
                .FirstOrDefault() ?? currentSy;

            decimal? Avg(IEnumerable<decimal?> xs) =>
                xs.Any(v => v.HasValue) ? Math.Round(xs.Where(v => v.HasValue).Average(v => v!.Value), 2)
                                        : (decimal?)null;

            decimal?[] MakeSeries(IEnumerable<dynamic> rowsForSem) => new decimal?[]
            {
                Avg(rowsForSem.Select(r => (decimal?)r.Prelims)),
                Avg(rowsForSem.Select(r => (decimal?)r.Midterm)),
                Avg(rowsForSem.Select(r => (decimal?)r.SemiFinal)),
                Avg(rowsForSem.Select(r => (decimal?)r.Final))
            };

            var rowsLatest = gradeRows.Where(r => string.Equals(r.SchoolYear, latestSy, StringComparison.OrdinalIgnoreCase)).ToList();
            var sem1Series = MakeSeries(rowsLatest.Where(r => string.Equals(r.SemShort, "1st", StringComparison.OrdinalIgnoreCase)));
            var sem2Series = MakeSeries(rowsLatest.Where(r => string.Equals(r.SemShort, "2nd", StringComparison.OrdinalIgnoreCase)));

            // ---------------- VM ----------------
            return new StudentDashboardViewModel
            {
                StudentName = $"{student.User.FirstName} {student.User.LastName}",
                Program = student.Program,
                YearLevel = FormatYearLevel(student.YearLevel),
                CumulativeGwa = cumulativeGwa,
                CurrentTermUnits = currentTermUnits,
                IsDeanListEligible = isDeanList,
                CurrentSchoolYear = currentSy,
                CurrentSemesterName = currentSemNameShort + " Semester",
                Sem1Series = sem1Series,
                Sem2Series = sem2Series,
                Sem1Label = "1st Semester",
                Sem2Label = "2nd Semester"
            };
        }

        // ------- helpers (same behavior as your controller) -------
        private static string GetCurrentSchoolYear()
        {
            var now = DateTime.Now;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        private static string GetCurrentSemesterName()
        {
            var now = DateTime.Now;
            if (now.Month is >= 6 and <= 10) return "1st";
            if (now.Month is >= 11 || now.Month <= 3) return "2nd";
            return "Mid";
        }

        private static string? ExtractSchoolYear(string? semesterText)
        {
            if (string.IsNullOrWhiteSpace(semesterText)) return null;
            var m = Regex.Match(semesterText, @"(20\d{2})\D+(20\d{2})");
            return m.Success ? $"{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
        }

        private static string FormatYearLevel(string? yearLevel)
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

        private static bool MatchesCurrentTerm(string? acSemester, string currentSy, string currentSemName)
        {
            if (string.IsNullOrWhiteSpace(acSemester)) return false;
            var syMatch = Regex.IsMatch(acSemester, Regex.Escape(currentSy));
            var semMatch = acSemester.Contains(currentSemName, StringComparison.OrdinalIgnoreCase);
            return syMatch || semMatch;
        }
    }
}
