using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class StudentGradesService : IStudentGradesService
    {
        private readonly IGradeRepository _grades;
        private readonly IStudentRepository _students;
        private readonly IUserRepository _users;

        public StudentGradesService(
            IGradeRepository grades,
            IStudentRepository students,
            IUserRepository users)
        {
            _grades = grades;
            _students = students;
            _users = users;
        }

        public async Task<StudentGradesViewModel> BuildAsync(
            int userId,
            string schoolYear,
            string semester,
            CancellationToken ct = default)
        {
            var user = await _users.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId, ct);

            var defaultSchoolYear = GetCurrentSchoolYear();
            var vm = new StudentGradesViewModel
            {
                StudentName = user != null ? $"{user.FirstName} {user.LastName}" : "Student",
                SchoolYear = defaultSchoolYear
            };

            var student = await _students.GetStudents()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId, ct);

            if (student == null)
                return vm;

            vm.Program = student.Program;
            vm.Department = student.Department;
            vm.YearLevel = FormatYearLevel(student.YearLevel);
            vm.Section = student.Section;   // from Students table

            // ---- raw grade rows from DB ----
            var rows = await _grades.GetGrades()
                .Where(g => g.StudentId == student.StudentId)
                .Select(g => new
                {
                    g.Prelims,
                    g.Midterm,
                    g.SemiFinal,
                    g.Final,
                    AC = g.AssignedCourse,
                    Course = g.AssignedCourse.Course,
                    TeacherUser = g.AssignedCourse.Teacher.User
                })
                .AsNoTracking()
                .ToListAsync(ct);

            // ---- map to VM rows + compute per-subject remarks ----
            var gradeRows = rows.Select(r =>
            {
                var rowSemester = r.AC?.Semester ?? "N/A";
                var rowSchoolYear = r.AC?.SchoolYear ?? defaultSchoolYear;

                var remarks = CalculateRemarksFromGrades(
                    r.Prelims,
                    r.Midterm,
                    r.SemiFinal,
                    r.Final);

                return new StudentGradeRowViewModel
                {
                    SubjectCode = r.AC?.EDPCode,
                    Description = r.Course?.Description,
                    Instructor = r.TeacherUser != null
                        ? $"{r.TeacherUser.FirstName} {r.TeacherUser.LastName}"
                        : "N/A",
                    Type = r.AC?.Type,
                    Units = UnitsForType(
                        r.AC?.Type,
                        r.AC?.Units,
                        r.Course?.LecUnits,
                        r.Course?.LabUnits,
                        r.Course?.TotalUnits),
                    Prelims = r.Prelims,
                    Midterm = r.Midterm,
                    SemiFinal = r.SemiFinal,
                    Final = r.Final,
                    Remarks = remarks,
                    Semester = rowSemester,
                    SchoolYear = rowSchoolYear
                };
            }).ToList();

            if (!gradeRows.Any())
                return vm;

            // ---- school year + semester options ----
            var availableSchoolYears = gradeRows
                .Select(r => r.SchoolYear)
                .Where(sy => !string.IsNullOrWhiteSpace(sy))
                .Distinct()
                .OrderBy(sy => sy)
                .ToList();

            if (!availableSchoolYears.Any())
                availableSchoolYears.Add(defaultSchoolYear);

            // use explicitly requested schoolYear or latest available
            var selectedSchoolYear = availableSchoolYears
                .FirstOrDefault(sy => string.Equals(sy, schoolYear, StringComparison.OrdinalIgnoreCase))
                ?? availableSchoolYears.Last();

            var availableSemesters = gradeRows
                .Where(r => string.Equals(r.SchoolYear, selectedSchoolYear, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Semester)
                .Where(se => !string.IsNullOrWhiteSpace(se))
                .Distinct()
                .OrderBy(se => se) // "1st Semester", "2nd Semester", etc.
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

            string selectedSemester;

            // if caller provided semester, respect it; otherwise default to latest
            if (!string.IsNullOrWhiteSpace(semester))
            {
                selectedSemester = availableSemesters
                    .FirstOrDefault(se => string.Equals(se, semester, StringComparison.OrdinalIgnoreCase))
                    ?? availableSemesters.LastOrDefault();
            }
            else
            {
                selectedSemester = availableSemesters.LastOrDefault();
            }

            // ---- filter rows for selected term ----
            var filteredGrades = gradeRows
                .Where(r =>
                    (string.IsNullOrWhiteSpace(selectedSchoolYear) ||
                        string.Equals(r.SchoolYear, selectedSchoolYear, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(selectedSemester) ||
                        string.Equals(r.Semester, selectedSemester, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // ---- overall GPA: average of per-subject GPAs ----
            var subjectGpas = filteredGrades
                .Select(r => ComputeGpa(r.Prelims, r.Midterm, r.SemiFinal, r.Final))
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToList();

            vm.AvailableSchoolYears = availableSchoolYears;
            vm.AvailableSemesters = availableSemesters;
            vm.SelectedSchoolYear = selectedSchoolYear;
            vm.SelectedSemester = selectedSemester;
            vm.SchoolYear = selectedSchoolYear ?? defaultSchoolYear;
            vm.Semester = selectedSemester ?? vm.Semester;
            vm.Grades = filteredGrades;
            vm.Gpa = subjectGpas.Any()
                ? decimal.Round(subjectGpas.Average(), 2)
                : (decimal?)null;

            return vm;
        }

        private static int UnitsForType(string type, int? acUnits, int? lecUnits, int? labUnits, int? totalUnits)
        {
            var t = (type ?? "").Trim().ToLowerInvariant();

            if (t == "lecture")
                return lecUnits ??
                       (totalUnits.HasValue && labUnits.HasValue
                            ? totalUnits.Value - labUnits.Value
                            : acUnits ?? 0);

            if (t == "laboratory" || t == "lab")
                return labUnits ??
                       (totalUnits.HasValue && lecUnits.HasValue
                            ? totalUnits.Value - lecUnits.Value
                            : acUnits ?? 0);

            return totalUnits ?? acUnits ?? 0;
        }

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

        private static decimal? ComputeGpa(decimal? prelims, decimal? midterm, decimal? semiFinal, decimal? final)
        {
            // If any is missing => incomplete
            if (!prelims.HasValue || !midterm.HasValue || !semiFinal.HasValue || !final.HasValue)
                return null;

            return Math.Round(
                (prelims.Value + midterm.Value + semiFinal.Value + final.Value) / 4m,
                2);
        }

        private static string CalculateRemarksFromGrades(decimal? prelims, decimal? midterm, decimal? semiFinal, decimal? final)
        {
            var gpa = ComputeGpa(prelims, midterm, semiFinal, final);
            if (!gpa.HasValue)
                return "INCOMPLETE";

            return gpa.Value <= 3.0m ? "PASSED" : "FAILED";
        }
    }
}
