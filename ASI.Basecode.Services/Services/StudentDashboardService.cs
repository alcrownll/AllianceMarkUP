using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class StudentDashboardService : IStudentDashboardService
    {
        private readonly AsiBasecodeDBContext _ctx;
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUserRepository _userRepository;

        public StudentDashboardService(
            AsiBasecodeDBContext ctx,
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            IUserRepository userRepository)
        {
            _ctx = ctx;
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

            // -------- Load Grades + related courses --------
            var grades = await _ctx.Grades
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.AssignedCourse)
                    .ThenInclude(ac => ac.Course)
                .AsNoTracking()
                .ToListAsync();

            // =============== GWA computation ===================
            decimal? cumulativeGwa = null;
            var finals = grades
                .Where(g => g.Final.HasValue)
                .Select(g => g.Final!.Value)
                .ToList();
            if (finals.Any())
                cumulativeGwa = Math.Round((decimal)finals.Average(), 2);

            var isDeanList = cumulativeGwa is >= 1.00m and <= 1.70m;

            // =============== TERM UNIT COMPUTATION ===================
            var termRows = grades
                .Where(g => g.AssignedCourse != null)
                .Select(g =>
                {
                    var ac = g.AssignedCourse!;
                    var (sy, semShort) = ParseTerm(ac.Semester);
                    sy ??= ac.SchoolYear ?? GetCurrentSchoolYear();
                    semShort ??= GetCurrentSemesterName();

                    int units = ac.Units > 0
                        ? ac.Units
                        : (ac.Course != null
                            ? ac.Course.LecUnits + ac.Course.LabUnits
                            : 0);

                    return new
                    {
                        SchoolYear = sy,
                        SemShort = semShort,
                        Units = units
                    };
                })
                .ToList();

            if (!termRows.Any())
            {
                return new StudentDashboardViewModel
                {
                    StudentName = $"{student.User.FirstName} {student.User.LastName}",
                    Program = student.Program,
                    YearLevel = FormatYearLevel(student.YearLevel),
                    CumulativeGwa = cumulativeGwa,
                    CurrentTermUnits = 0,
                    IsDeanListEligible = isDeanList,
                    CurrentSchoolYear = GetCurrentSchoolYear(),
                    CurrentSemesterName = GetCurrentSemesterName() + " Semester",
                    Sem1Series = Array.Empty<decimal?>(),
                    Sem2Series = Array.Empty<decimal?>(),
                    Sem1Label = "1st Semester",
                    Sem2Label = "2nd Semester"
                };
            }

            var latestSy = termRows
                .Select(t => t.SchoolYear)
                .OrderByDescending(s => s)
                .First();

            int units1 = termRows
                .Where(t => t.SchoolYear == latestSy && t.SemShort.Equals("1st", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Units);
            int units2 = termRows
                .Where(t => t.SchoolYear == latestSy && t.SemShort.Equals("2nd", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Units);

            string targetSemShort = units1 >= units2 ? "1st" : "2nd";
            int currentTermUnits = Math.Max(units1, units2);
            var targetSy = latestSy;

            // =============== CHART SERIES ===================
            var gradeRows = grades.Select(g =>
            {
                var semText = g.AssignedCourse?.Semester ?? "";
                var (sy, semShort) = ParseTerm(semText);
                sy ??= targetSy;
                semShort ??= targetSemShort;

                return new
                {
                    SchoolYear = sy!,
                    SemShort = semShort!,
                    g.Prelims,
                    g.Midterm,
                    g.SemiFinal,
                    g.Final
                };
            }).ToList();

            decimal? Avg(IEnumerable<decimal?> xs) =>
                xs.Any(v => v.HasValue)
                    ? Math.Round(xs.Where(v => v.HasValue).Average(v => v!.Value), 2)
                    : (decimal?)null;

            decimal?[] MakeSeries(IEnumerable<dynamic> rowsForSem) => new decimal?[]
            {
                Avg(rowsForSem.Select(r => (decimal?)r.Prelims)),
                Avg(rowsForSem.Select(r => (decimal?)r.Midterm)),
                Avg(rowsForSem.Select(r => (decimal?)r.SemiFinal)),
                Avg(rowsForSem.Select(r => (decimal?)r.Final))
            };

            var sem1Series = MakeSeries(gradeRows.Where(r => r.SemShort == "1st"));
            var sem2Series = MakeSeries(gradeRows.Where(r => r.SemShort == "2nd"));

            return new StudentDashboardViewModel
            {
                StudentName = $"{student.User.FirstName} {student.User.LastName}",
                Program = student.Program,
                YearLevel = FormatYearLevel(student.YearLevel),
                CumulativeGwa = cumulativeGwa,
                CurrentTermUnits = currentTermUnits,  
                IsDeanListEligible = isDeanList,
                CurrentSchoolYear = targetSy,
                CurrentSemesterName = targetSemShort + " Semester",
                Sem1Series = sem1Series,
                Sem2Series = sem2Series,
                Sem1Label = "1st Semester",
                Sem2Label = "2nd Semester"
            };
        }

        // ---------------- Helpers ----------------
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

        private static (string? sy, string? semShort) ParseTerm(string? semesterText)
        {
            if (string.IsNullOrWhiteSpace(semesterText))
                return (null, null);

            var m = Regex.Match(semesterText, @"(20\d{2})\D+(20\d{2})");
            var sy = m.Success ? $"{m.Groups[1].Value}-{m.Groups[2].Value}" : null;

            var txt = semesterText.ToLowerInvariant();
            string? sem =
                txt.Contains("mid") ? "Mid" :
                (txt.Contains("2nd") || txt.Contains("second")) ? "2nd" :
                (txt.Contains("1st") || txt.Contains("first")) ? "1st" :
                null;

            return (sy, sem);
        }

        private static string FormatYearLevel(string? yearLevel)
        {
            if (string.IsNullOrWhiteSpace(yearLevel)) return "N/A";
            if (!int.TryParse(yearLevel, out var level)) return yearLevel;
            return level switch
            {
                1 => "1st Year",
                2 => "2nd Year",
                3 => "3rd Year",
                4 => "4th Year",
                _ => $"{level}th Year"
            };
        }
    }
}
