using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

        public async Task<StudentGradesViewModel> BuildAsync(int userId, string schoolYear, string semester, CancellationToken ct = default)
        {
            // Resolve user + student
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

            // Pull all grades with required joins; project to lightweight shape
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

            var gradeRows = rows.Select(r =>
            {
                var rowSemester = r.AC?.Semester ?? "N/A";       // "1st Semester"
                var rowSchoolYear = r.AC?.SchoolYear ?? defaultSchoolYear; // "2025-2026"
                var remarks = CalculateRemarksFromGrades(r.Prelims, r.Midterm, r.SemiFinal, r.Final);

                return new StudentGradeRowViewModel
                {
                    SubjectCode = r.AC?.EDPCode,
                    Description = r.Course?.Description,
                    Instructor = r.TeacherUser != null ? $"{r.TeacherUser.FirstName} {r.TeacherUser.LastName}" : "N/A",
                    Type = r.AC?.Type,
                    Units = r.Course?.TotalUnits ?? r.AC?.Units ?? 0,
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

            // Build filters (school years / semesters)
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
                ?? availableSchoolYears.First();

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

            vm.AvailableSchoolYears = availableSchoolYears;
            vm.AvailableSemesters = availableSemesters;
            vm.SelectedSchoolYear = selectedSchoolYear;
            vm.SelectedSemester = selectedSemester;
            vm.SchoolYear = selectedSchoolYear ?? defaultSchoolYear;
            vm.Semester = selectedSemester ?? vm.Semester;
            vm.Grades = filteredGrades;
            vm.Gpa = finals.Any() ? decimal.Round(finals.Average(), 2) : (decimal?)null;

            return vm;
        }

        // -------- helpers (kept here so controller stays thin) --------

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

        private static string CalculateRemarksFromGrades(decimal? prelims, decimal? midterm, decimal? semiFinal, decimal? final)
        {
            var components = new (decimal? Score, decimal Weight)[] {
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
    }
}
