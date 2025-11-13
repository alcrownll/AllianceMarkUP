using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class StudyLoadService : IStudyLoadService
    {
        private readonly AsiBasecodeDBContext _ctx;

        public StudyLoadService(AsiBasecodeDBContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<StudyLoadViewModel> GetStudyLoadAsync(int userId, string termValue)
        {
            // Resolve student
            var student = await _ctx.Students
                .Include(s => s.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
                return new StudyLoadViewModel { SelectedTerm = termValue, Terms = new List<TermItem>() };

            // term list from AssignedCourses joined via Grades
            var rawTerms = await _ctx.Grades
                .Where(g => g.StudentId == student.StudentId && g.AssignedCourse != null)
                .Select(g => new
                {
                    SchoolYear = g.AssignedCourse.SchoolYear,     // e.g., "2025-2026"
                    SemesterText = g.AssignedCourse.Semester      // e.g., "1st Semester"
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.SchoolYear) && !string.IsNullOrWhiteSpace(x.SemesterText))
                .Distinct()
                .ToListAsync();

            // Map to value "YYYY-YYYY-<1|2>
            var terms = rawTerms
                .Select(x => new
                {
                    x.SchoolYear,
                    SemesterNum = SemTextToNum(x.SemesterText),
                    x.SemesterText
                })
                .Where(x => x.SemesterNum != null)
                .OrderBy(x => x.SchoolYear)
                .ThenBy(x => x.SemesterNum)
                .Select(x => new TermItem
                {
                    Value = $"{x.SchoolYear}-{x.SemesterNum}",                    
                    Text = $"S.Y. {x.SchoolYear} - {x.SemesterText}",
                    Selected = false
                })
                .ToList();

            if (terms.Count == 0)
            {
                return new StudyLoadViewModel
                {
                    StudentName = $"{student.User.FirstName} {student.User.LastName}",
                    Program = student.Program,
                    YearLevel = student.YearLevel?.ToString(),
                    SelectedTerm = termValue,
                    Terms = new List<TermItem>(),
                    Rows = new List<StudyLoadRow>()
                };
            }


            string selected = termValue;
            if (string.IsNullOrWhiteSpace(selected) || !terms.Any(t => t.Value.Equals(selected, StringComparison.OrdinalIgnoreCase)))
            {
                var currentSy = GetCurrentSchoolYear();
                var currentSemNum = GetCurrentSemesterNum(); // 1 or 2

                var currentValue = $"{currentSy}-{currentSemNum}";
                selected = terms.Any(t => t.Value == currentValue)
                    ? currentValue
                    : terms.OrderBy(t => t.Value).Last().Value; // latest
            }

            foreach (var t in terms) t.Selected = t.Value.Equals(selected, StringComparison.OrdinalIgnoreCase);
            var (sy, semNum) = ParseTerm(selected);
            var semText = SemNumToText(semNum);

            var acIds = await _ctx.Grades
                .Where(g => g.StudentId == student.StudentId
                         && g.AssignedCourse != null
                         && g.AssignedCourse.SchoolYear == sy
                         && g.AssignedCourse.Semester == semText)
                .Select(g => g.AssignedCourseId)
                .Distinct()
                .ToListAsync();

            var assigned = await _ctx.AssignedCourses
                .Where(ac => acIds.Contains(ac.AssignedCourseId))
                .Include(ac => ac.Course)
                .Include(ac => ac.Teacher).ThenInclude(t => t.User)
                .AsNoTracking()
                .ToListAsync();

            var schedules = await _ctx.ClassSchedules
                .Where(cs => acIds.Contains(cs.AssignedCourseId))
                .AsNoTracking()
                .ToListAsync();

            var schedLookup = schedules
                .GroupBy(s => s.AssignedCourseId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<StudyLoadRow>();
            foreach (var ac in assigned)
            {
                schedLookup.TryGetValue(ac.AssignedCourseId, out var schedsForCourse);

                rows.Add(new StudyLoadRow
                {
                    EDPCode = ac.EDPCode,
                    Subject = ac.Course?.CourseCode ?? "",
                    Description = ac.Course?.Description ?? "",
                    Instructor = ac.Teacher?.User != null
                                ? $"{ac.Teacher.User.FirstName} {ac.Teacher.User.LastName}"
                                : "",
                    Units = ac.Units > 0
                                ? ac.Units
                                : (ac.Course != null ? (ac.Course.LabUnits + ac.Course.LecUnits) : 0),
                    Type = ac.Type,
                    Room = schedsForCourse?.FirstOrDefault()?.Room ?? "",
                    DateTime = FormatSchedule(schedsForCourse)
                });
            }

            return new StudyLoadViewModel
            {
                StudentName = $"{student.User.FirstName} {student.User.LastName}",
                Program = student.Program,
                YearLevel = student.YearLevel?.ToString(),
                SelectedTerm = selected,      
                Terms = terms,               
                Rows = rows.OrderBy(r => r.Subject).ThenBy(r => r.Type).ToList()
            };
        }

        // ---------- helpers ----------

        private static (string sy, int semNum) ParseTerm(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return (null, 1);
            var parts = value.Split('-');
            if (parts.Length < 3) return (value, 1);
            return ($"{parts[0]}-{parts[1]}", int.TryParse(parts[2], out var n) ? n : 1);
        }

        private static int GetCurrentSemesterNum()
        {
            var now = DateTime.Now;
            if (now.Month is >= 6 and <= 10) return 1;    
            if (now.Month is >= 11 || now.Month <= 3) return 2; 
            return 1;
        }

        private static string GetCurrentSchoolYear()
        {
            var now = DateTime.Now;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

        private static int? SemTextToNum(string semText)
        {
            if (string.IsNullOrWhiteSpace(semText)) return null;
            semText = semText.Trim().ToLowerInvariant();
            if (semText.Contains("1st")) return 1;
            if (semText.Contains("first")) return 1;
            if (semText.Contains("2nd")) return 2;
            if (semText.Contains("second")) return 2;
            return null;
        }

        private static string SemNumToText(int semNum)
        {
            return semNum == 2 ? "2nd Semester" : "1st Semester";
        }

        private static string FormatSchedule(ICollection<ClassSchedule> scheds)
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
    }
}
