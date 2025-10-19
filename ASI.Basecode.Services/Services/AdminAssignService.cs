using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class AdminAssignService : IAdminAssignService
    {
        private readonly IAssignedCourseRepository _assigned;
        private readonly ICourseRepository _courses;
        private readonly ITeacherRepository _teachers;
        private readonly IProgramRepository _programs;
        private readonly IStudentRepository _students;
        private readonly IGradeRepository _grades;
        private readonly IClassScheduleRepository _classSchedules;

        public AdminAssignService(
            IAssignedCourseRepository assigned,
            ICourseRepository courses,
            ITeacherRepository teachers,
            IProgramRepository programs,
            IStudentRepository students,
            IGradeRepository grades,
            IClassScheduleRepository classSchedules)
        {
            _assigned = assigned;
            _courses = courses;
            _teachers = teachers;
            _programs = programs;
            _students = students;
            _grades = grades;
            _classSchedules = classSchedules;
        }

        public async Task<IReadOnlyList<AssignedCourse>> GetListAsync(string q = null)
        {
            IQueryable<AssignedCourse> query = _assigned.GetAssignedCourses()
                .AsNoTracking()
                .Include(a => a.Course)
                .Include(a => a.Teacher).ThenInclude(t => t.User);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(a =>
                    (a.EDPCode ?? "").ToLower().Contains(term) ||
                    (a.Course != null && (a.Course.CourseCode ?? "").ToLower().Contains(term)) ||
                    (a.Teacher != null && a.Teacher.User != null &&
                        (
                          ((a.Teacher.User.FirstName ?? "") + " " + (a.Teacher.User.LastName ?? "")).ToLower().Contains(term) ||
                          (a.Teacher.User.LastName ?? "").ToLower().Contains(term) ||
                          (a.Teacher.User.FirstName ?? "").ToLower().Contains(term)
                        )
                    )
                );
            }

            return await query
                .OrderBy(a => a.EDPCode)
                .ThenBy(a => a.Course.CourseCode)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Course>> GetCoursesAsync() =>
            await _courses.GetCourses().AsNoTracking().OrderBy(c => c.CourseCode).ToListAsync();

        public async Task<IReadOnlyList<Program>> GetProgramsAsync() =>
            await _programs.GetPrograms().AsNoTracking().OrderBy(p => p.ProgramCode).ToListAsync();

        public async Task<IReadOnlyList<Teacher>> GetTeachersWithUsersAsync() =>
            await _teachers.GetTeachers()
                .AsNoTracking()
                .Include(t => t.User)
                .OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName)
                .ToListAsync();

        public async Task<PagedResultModel<Student>> GetStudentsForAssignAsync(
            string program, string yearLevel, string section, string status,
            int page, int pageSize, CancellationToken ct)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();

            var q = _students.GetStudentsWithUser()
                .AsNoTracking()
                .Where(s => s.User.AccountStatus == normalizedStatus);

            bool hasProg = !string.IsNullOrWhiteSpace(program);
            bool hasYear = !string.IsNullOrWhiteSpace(yearLevel);
            bool hasSec = !string.IsNullOrWhiteSpace(section);

            if (hasProg || hasYear || hasSec)
            {
                // NOTE: this filter shows all NOT in the selected block (based on your previous code)
                q = q.Where(s =>
                    !((!hasProg || s.Program == program) &&
                      (!hasYear || s.YearLevel == yearLevel) &&
                      (!hasSec || s.Section == section)));
            }

            q = q.OrderBy(s => s.Program)
                 .ThenBy(s => s.YearLevel)
                 .ThenBy(s => s.User.LastName)
                 .ThenBy(s => s.User.FirstName)
                 .ThenBy(s => s.User.UserId);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .Include(s => s.User)
                               .ToListAsync(ct);

            return new PagedResultModel<Student>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<string> GenerateEdpCodeAsync(int courseId, string semester, string schoolYear, CancellationToken ct = default)
        {
            var rng = new Random();
            for (int i = 0; i < 50; i++)
            {
                string candidate = rng.Next(10000, 100000).ToString(); // 5 digits
                bool exists = await _assigned.GetAssignedCourses()
                    .AsNoTracking()
                    .AnyAsync(a => a.EDPCode == candidate, ct);

                if (!exists)
                    return candidate;
            }

            int fallback = (int)((DateTime.UtcNow.Ticks % 90000) + 10000);
            return fallback.ToString();
        }

        // helpers

        private static bool TryParseTime(string hhmm, out TimeSpan ts)
        {
            ts = default;
            if (string.IsNullOrWhiteSpace(hhmm))
                return false;

            return TimeSpan.TryParse(hhmm, out ts);
        }

        private static void ValidateTimeWindow(TimeSpan start, TimeSpan end)
        {
            var min = new TimeSpan(7, 30, 0);   // 07:30 AM
            var max = new TimeSpan(21, 30, 0);  // 9:30 PM
            if (start < min || start > max) throw new InvalidOperationException("Start time must be between 07:30 and 21:30.");
            if (end < min || end > max) throw new InvalidOperationException("End time must be between 07:30 and 21:30.");
            if (end <= start) throw new InvalidOperationException("End time must be later than start time.");
        }

        private static HashSet<int> ParseDaysCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => int.TryParse(s, out var d) ? d : -1)
                      .Where(d => d >= 1 && d <= 6)
                      .ToHashSet();
        }

        /// <summary>
        /// Lenient schedule creation: only writes when room + start + end + at least one day are provided.
        /// Returns false if it no-ops; true if it wrote schedules.
        /// </summary>
        private async Task<bool> TryCreateSchedulesLenientAsync(
            int assignedCourseId,
            string room,
            string startHHmm,
            string endHHmm,
            string daysCsv,
            CancellationToken ct)
        {
            var daySet = ParseDaysCsv(daysCsv);
            if (daySet.Count == 0) return false;
            if (string.IsNullOrWhiteSpace(room)) return false;
            if (!TryParseTime(startHHmm, out var start)) return false;
            if (!TryParseTime(endHHmm, out var end)) return false;

            ValidateTimeWindow(start, end);
            var trimmedRoom = room.Trim();

            foreach (var d in daySet)
            {
                _classSchedules.AddClassSchedule(new ClassSchedule
                {
                    AssignedCourseId = assignedCourseId,
                    Day = (DayOfWeek)d, // Mon=1..Sat=6
                    StartTime = start,
                    EndTime = end,
                    Room = trimmedRoom
                });
            }
            await _classSchedules.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Lenient upsert: 
        /// - If inputs are incomplete OR no days => delete existing schedules for this course (reflects empty selection) and no-op insert.
        /// - Otherwise upsert all specified days and delete the rest.
        /// </summary>
        private async Task UpsertSchedulesLenientAsync(
            int assignedCourseId,
            string room,
            string startHHmm,
            string endHHmm,
            string daysCsv,
            CancellationToken ct)
        {
            var want = ParseDaysCsv(daysCsv);

            // Incomplete => remove all schedules (treat as user cleared the selection)
            if (want.Count == 0 ||
                string.IsNullOrWhiteSpace(room) ||
                !TryParseTime(startHHmm, out var start) ||
                !TryParseTime(endHHmm, out var end))
            {
                var existingAll = await _classSchedules.GetClassSchedules()
                    .Where(s => s.AssignedCourseId == assignedCourseId)
                    .ToListAsync(ct);

                foreach (var del in existingAll)
                    _classSchedules.DeleteClassSchedule(del.ClassScheduleId);

                await _classSchedules.SaveChangesAsync(ct);
                return;
            }

            ValidateTimeWindow(start, end);
            var trimmedRoom = room.Trim();

            var existing = await _classSchedules.GetClassSchedules()
                .Where(s => s.AssignedCourseId == assignedCourseId)
                .ToListAsync(ct);

            // Update overlapping days
            foreach (var row in existing.Where(r => want.Contains((int)r.Day)))
            {
                row.Room = trimmedRoom;
                row.StartTime = start;
                row.EndTime = end;
                _classSchedules.UpdateClassSchedule(row);
            }

            // Insert missing days
            var have = existing.Select(e => (int)e.Day).ToHashSet();
            foreach (var d in want.Except(have))
            {
                _classSchedules.AddClassSchedule(new ClassSchedule
                {
                    AssignedCourseId = assignedCourseId,
                    Day = (DayOfWeek)d,
                    StartTime = start,
                    EndTime = end,
                    Room = trimmedRoom
                });
            }

            // Delete removed days
            foreach (var del in existing.Where(r => !want.Contains((int)r.Day)).ToList())
                _classSchedules.DeleteClassSchedule(del.ClassScheduleId);

            await _classSchedules.SaveChangesAsync(ct);
        }

        private async Task SeedGradesForTargetsAsync(int assignedCourseId, IEnumerable<int> targetStudentIds, CancellationToken ct)
        {
            var set = (targetStudentIds ?? Enumerable.Empty<int>()).Where(i => i > 0).ToHashSet();
            if (set.Count == 0) return;

            var gradeRows = set.Select(sid => new Grade
            {
                StudentId = sid,
                AssignedCourseId = assignedCourseId,
                Prelims = null,
                Midterm = null,
                SemiFinal = null,
                Final = null
            }).ToList();

            _grades.AddGradesNoSave(gradeRows);
            await _grades.SaveChangesAsync(ct);
        }

        private async Task<HashSet<int>> CollectTargetStudentIdsAsync(
            string blockProgram, string blockYear, string blockSection,
            IEnumerable<int> extraStudentIds, CancellationToken ct)
        {
            var targetIds = new HashSet<int>();

            bool hasBlock = !string.IsNullOrWhiteSpace(blockProgram)
                         && !string.IsNullOrWhiteSpace(blockYear)
                         && !string.IsNullOrWhiteSpace(blockSection);

            if (hasBlock)
            {
                var inBlock = await _students.GetStudentsWithUser()
                    .AsNoTracking()
                    .Where(s => s.Program == blockProgram
                             && s.YearLevel == blockYear
                             && s.Section == blockSection)
                    .Select(s => s.StudentId)
                    .ToListAsync(ct);

                foreach (var id in inBlock) targetIds.Add(id);
            }

            if (extraStudentIds != null)
            {
                foreach (var id in extraStudentIds.Where(i => i > 0))
                    targetIds.Add(id);
            }

            return targetIds;
        }

        // ===================== Create (Assigned + optional Schedules + optional Grades) =====================

        public async Task<int> CreateAssignedCourseAsync(
            AssignedCourse form,
            string blockProgram,
            string blockYear,
            string blockSection,
            IEnumerable<int> extraStudentIds,
            string scheduleRoom,
            string scheduleStartHHmm,
            string scheduleEndHHmm,
            string scheduleDaysCsv,
            CancellationToken ct = default)
        {
            // Assigned course base creation
            var ac = new AssignedCourse
            {
                EDPCode = string.IsNullOrWhiteSpace(form.EDPCode)
                                ? await GenerateEdpCodeAsync(form.CourseId, form.Semester, form.SchoolYear, ct)
                                : form.EDPCode.Trim(),
                CourseId = form.CourseId,
                Type = form.Type,
                Units = form.Units,
                ProgramId = form.ProgramId,
                TeacherId = form.TeacherId,
                Semester = form.Semester,
                SchoolYear = form.SchoolYear,
            };

            _assigned.AddAssignedCourseNoSave(ac);
            await _assigned.SaveChangesAsync(ct);

            // Seed grades like your working flow (no error if 0 students)
            var targets = await CollectTargetStudentIdsAsync(blockProgram, blockYear, blockSection, extraStudentIds, ct);
            await SeedGradesForTargetsAsync(ac.AssignedCourseId, targets, ct);

            // Try schedules (lenient): only writes when complete; silent no-op otherwise
            await TryCreateSchedulesLenientAsync(ac.AssignedCourseId, scheduleRoom, scheduleStartHHmm, scheduleEndHHmm, scheduleDaysCsv, ct);

            return ac.AssignedCourseId;
        }

        // ===================== Reads =====================

        public async Task<AssignedCourse> GetAssignedCourseAsync(int id, CancellationToken ct)
        {
            return await _assigned.GetAssignedCourses()
                .Include(a => a.Course)
                .Include(a => a.Teacher).ThenInclude(t => t.User)
                .Include(a => a.Program)
                .FirstOrDefaultAsync(a => a.AssignedCourseId == id, ct);
        }

        public async Task<IReadOnlyList<Student>> GetEnrolledStudentsAsync(int assignedCourseId, CancellationToken ct)
        {
            var ids = await _grades.GetGrades()
                .AsNoTracking()
                .Where(g => g.AssignedCourseId == assignedCourseId)
                .Select(g => g.StudentId)
                .ToListAsync(ct);

            return await _students.GetStudentsWithUser()
                .Where(s => ids.Contains(s.StudentId))
                .OrderBy(s => s.User.LastName).ThenBy(s => s.User.FirstName)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ClassSchedule>> GetSchedulesAsync(int assignedCourseId, CancellationToken ct)
        {
            return await _classSchedules.GetClassSchedules()
                .AsNoTracking()
                .Where(s => s.AssignedCourseId == assignedCourseId)
                .OrderBy(s => s.Day)
                .ToListAsync(ct);
        }

        public async Task<(IReadOnlyList<Student> Items, int Total)> GetAddableStudentsPageAsync(
            int assignedCourseId,
            string blockProgram,
            string blockYear,
            string blockSection,
            string status,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var enrolledIds = await _grades.GetGrades()
                                   .AsNoTracking()
                                   .Where(g => g.AssignedCourseId == assignedCourseId)
                                   .Select(g => g.StudentId)
                                   .ToListAsync(ct);
            var enrolledSet = enrolledIds.ToHashSet();

            var q = _students.GetStudentsWithUser();

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(s => s.User.AccountStatus == status);

            bool hasProg = !string.IsNullOrWhiteSpace(blockProgram);
            bool hasYear = !string.IsNullOrWhiteSpace(blockYear);
            bool hasSec = !string.IsNullOrWhiteSpace(blockSection);

            if (hasProg || hasYear || hasSec)
            {
                q = q.Where(s =>
                    !((!hasProg || s.Program == blockProgram) &&
                      (!hasYear || s.YearLevel == blockYear) &&
                      (!hasSec || s.Section == blockSection)));
            }

            q = q.Where(s => !enrolledSet.Contains(s.StudentId))
                 .OrderBy(s => s.Program)
                 .ThenBy(s => s.YearLevel)
                 .ThenBy(s => s.User.LastName)
                 .ThenBy(s => s.User.FirstName)
                 .ThenBy(s => s.User.UserId);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return (items, total);
        }

        // ===================== Update (Assigned + Students + Schedules) =====================

        public async Task UpdateAssignedCourseAsync(
            AssignedCourse posted,
            IEnumerable<int> removeStudentIds,
            IEnumerable<int> addStudentIds,
            string scheduleRoom,
            string scheduleStartHHmm,
            string scheduleEndHHmm,
            string scheduleDaysCsv,
            CancellationToken ct)
        {
            var ac = await _assigned.GetAssignedCourses()
                .FirstOrDefaultAsync(a => a.AssignedCourseId == posted.AssignedCourseId, ct);

            if (ac == null) throw new InvalidOperationException("Assigned course not found.");

            ac.EDPCode = posted.EDPCode?.Trim();
            ac.CourseId = posted.CourseId;
            ac.Type = posted.Type;
            ac.Units = posted.Units;
            ac.ProgramId = posted.ProgramId;
            ac.TeacherId = posted.TeacherId;
            ac.Semester = posted.Semester;
            ac.SchoolYear = posted.SchoolYear;

            _assigned.UpdateAssignedCourse(ac);

            // Remove students
            if (removeStudentIds != null)
            {
                var toRemove = await _grades.GetGrades()
                    .Where(g => g.AssignedCourseId == ac.AssignedCourseId && removeStudentIds.Contains(g.StudentId))
                    .ToListAsync(ct);

                foreach (var g in toRemove) _grades.DeleteGrade(g.GradeId);
                if (toRemove.Count > 0)
                    await _grades.SaveChangesAsync(ct);
            }

            // Add students (avoid dupes)
            if (addStudentIds != null)
            {
                var existing = await _grades.GetGrades()
                    .AsNoTracking()
                    .Where(g => g.AssignedCourseId == ac.AssignedCourseId && addStudentIds.Contains(g.StudentId))
                    .Select(g => g.StudentId)
                    .ToListAsync(ct);

                var newIds = addStudentIds.Except(existing).Distinct().ToList();
                if (newIds.Count > 0)
                {
                    var rows = newIds.Select(sid => new Grade
                    {
                        StudentId = sid,
                        AssignedCourseId = ac.AssignedCourseId,
                        Prelims = null,
                        Midterm = null,
                        SemiFinal = null,
                        Final = null
                    });
                    _grades.AddGradesNoSave(rows);
                    await _grades.SaveChangesAsync(ct);
                }
            }

            // Upsert schedules (lenient)
            await UpsertSchedulesLenientAsync(ac.AssignedCourseId, scheduleRoom, scheduleStartHHmm, scheduleEndHHmm, scheduleDaysCsv, ct);
        }

        // ===================== Delete =====================

        public async Task<(bool ok, string message)> DeleteAssignedCourseAsync(int assignedCourseId, CancellationToken ct)
        {
            var ac = await _assigned.GetAssignedCourses()
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.AssignedCourseId == assignedCourseId, ct);

            if (ac == null)
                return (false, "Assigned course not found.");

            var friendly = $"EDP {ac.EDPCode} – {ac.Course?.CourseCode ?? "N/A"}";

            var gradeRows = await _grades.GetGrades()
                .Where(g => g.AssignedCourseId == assignedCourseId)
                .ToListAsync(ct);
            foreach (var g in gradeRows)
                _grades.DeleteGrade(g.GradeId);
            if (gradeRows.Count > 0)
                await _grades.SaveChangesAsync(ct);

            var schedRows = await _classSchedules.GetClassSchedules()
                .Where(s => s.AssignedCourseId == assignedCourseId)
                .ToListAsync(ct);
            foreach (var s in schedRows)
                _classSchedules.DeleteClassSchedule(s.ClassScheduleId);

            _assigned.DeleteAssignedCourse(assignedCourseId);
            await _assigned.SaveChangesAsync(ct);

            return (true, $"{friendly} was successfully deleted.");
        }
    }
}
