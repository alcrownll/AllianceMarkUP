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
            // Resolve the current student via logged-in user
            var student = await _ctx.Students
                .Include(s => s.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
                return new StudyLoadViewModel { SelectedTerm = termValue, Terms = BuildTerms(termValue ?? GetDefaultTerm()) };

            // Expect "YYYY-YYYY-1" or "YYYY-YYYY-2"
            var selectedTerm = string.IsNullOrWhiteSpace(termValue) ? GetDefaultTerm() : termValue;

            // AssignedCourseIds via Grades (enrollment)
            var acIds = await _ctx.Grades
                .Where(g => g.StudentId == student.StudentId &&
                            g.AssignedCourse.Semester == selectedTerm)
                .Select(g => g.AssignedCourseId)
                .Distinct()
                .ToListAsync();

            // Load assigned courses (Course + Teacher.User)
            var assigned = await _ctx.AssignedCourses
                .Where(ac => acIds.Contains(ac.AssignedCourseId))
                .Include(ac => ac.Course)
                .Include(ac => ac.Teacher).ThenInclude(t => t.User)
                .AsNoTracking()
                .ToListAsync();

            // Load schedules separately and build lookup by AssignedCourseId
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
                SelectedTerm = selectedTerm,
                Terms = BuildTerms(selectedTerm),
                Rows = rows.OrderBy(r => r.Subject).ThenBy(r => r.Type).ToList()
            };
        }

        private static List<TermItem> BuildTerms(string selected)
        {
            // Generate current ±1 school years for demo
            var now = DateTime.UtcNow;
            int syStart = now.Month >= 6 ? now.Year : now.Year - 1;
            var candidates = new List<string>
            {
                $"{syStart-1}-{syStart}-1",
                $"{syStart-1}-{syStart}-2",
                $"{syStart}-{syStart+1}-1",
                $"{syStart}-{syStart+1}-2",
                $"{syStart+1}-{syStart+2}-1",
                $"{syStart+1}-{syStart+2}-2",
            };

            return candidates.Distinct().Select(v => new TermItem
            {
                Value = v,
                Text = ToPrettyTerm(v),
                Selected = string.Equals(v, selected, StringComparison.OrdinalIgnoreCase)
            }).ToList();
        }

        private static string GetDefaultTerm()
        {
            var now = DateTime.UtcNow;
            int syStart = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{syStart}-{syStart + 1}-1"; // default to First Sem
        }

        private static string ToPrettyTerm(string value)
        {
            // "2025-2026-1" => "S.Y. 2025-2026 - First Semester"
            if (string.IsNullOrWhiteSpace(value)) return value;
            var parts = value.Split('-');
            if (parts.Length < 3) return value;
            var sy = $"{parts[0]}-{parts[1]}";
            var sem = parts[2] == "1" ? "First Semester" : parts[2] == "2" ? "Second Semester" : parts[2];
            return $"S.Y. {sy} - {sem}";
        }

        // -------- Schedule formatting helpers (DayOfWeek + TimeSpan) --------

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

        private static string AbbrevDay(DayOfWeek day)
        {
            return day switch
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
        }

        private static string To12h(TimeSpan t)
        {
            var dt = DateTime.Today.Add(t);
            return dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }
    }
}
