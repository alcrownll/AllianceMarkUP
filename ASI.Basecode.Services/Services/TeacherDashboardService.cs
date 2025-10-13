using System;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class TeacherDashboardService : ITeacherDashboardService
    {
        private readonly IUserRepository _userRepo;
        private readonly ITeacherRepository _teacherRepo;
        private readonly IAssignedCourseRepository _assignedRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly IClassScheduleRepository _scheduleRepo;
        private readonly IGradeRepository _gradeRepo;

        public TeacherDashboardService(
            IUserRepository userRepo,
            ITeacherRepository teacherRepo,
            IAssignedCourseRepository assignedRepo,
            ICourseRepository courseRepo,
            IClassScheduleRepository scheduleRepo,
            IGradeRepository gradeRepo)
        {
            _userRepo = userRepo;
            _teacherRepo = teacherRepo;
            _assignedRepo = assignedRepo;
            _courseRepo = courseRepo;
            _scheduleRepo = scheduleRepo;
            _gradeRepo = gradeRepo;
        }

        public async Task<TeacherDashboardViewModel> BuildAsync(string idNumber)
        {
            // Identify the teacher
            var user = await _userRepo.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            if (user == null)
                return new TeacherDashboardViewModel { TeacherName = "Teacher" };

            var teacher = await _teacherRepo.GetTeachers()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == user.UserId);

            var teacherId = teacher?.TeacherId;

            if (teacherId == null)
                return new TeacherDashboardViewModel
                {
                    TeacherName = $"{user.FirstName} {user.LastName}",
                    TotalCourses = 0,
                    TotalStudents = 0,
                    GradedPct = 0,
                    Summary = new ProgramSummary()
                };

            // ===============================================
            // Fetch all assigned courses for this teacher
            // ===============================================
            var assignedRaw = await _assignedRepo.GetAssignedCourses()
                .AsNoTracking()
                .Where(a => a.TeacherId == teacherId)
                .Select(a => new
                {
                    a.AssignedCourseId,
                    a.CourseId,
                    a.Program // e.g. "BSCS" / "BSIT"
                })
                .ToListAsync();

            if (assignedRaw.Count == 0)
                return new TeacherDashboardViewModel
                {
                    TeacherName = $"{user.FirstName} {user.LastName}",
                    TotalCourses = 0,
                    TotalStudents = 0,
                    GradedPct = 0,
                    Summary = new ProgramSummary()
                };

            // Normalize "BSCS" → "CS", "BSIT" → "IT"
            var assigned = assignedRaw.Select(a => new
            {
                a.AssignedCourseId,
                a.CourseId,
                ProgramUi = NormalizeProgram(a.Program)
            }).ToList();

            var assignedIds = assigned.Select(a => a.AssignedCourseId).ToList();
            var courseIds = assigned.Select(a => a.CourseId).Distinct().ToList();

            // ===============================================
            // Fetch course details
            // ===============================================
            var courses = await _courseRepo.GetCourses()
                .AsNoTracking()
                .Where(c => courseIds.Contains(c.CourseId))
                .Select(c => new { c.CourseId, c.CourseCode, c.Description })
                .ToListAsync();

            // ===============================================
            // Roster per AssignedCourse via Grades
            // ===============================================
            var rosters = await _gradeRepo.GetGrades()
                .AsNoTracking()
                .Where(g => assignedIds.Contains(g.AssignedCourseId))
                .GroupBy(g => g.AssignedCourseId)
                .Select(g => new
                {
                    AssignedCourseId = g.Key,
                    Students = g.Select(x => x.StudentId).Distinct().Count()
                })
                .ToListAsync();

            // ===============================================
            // Grades progress
            // ===============================================
            var grades = await _gradeRepo.GetGrades()
                .AsNoTracking()
                .Where(g => assignedIds.Contains(g.AssignedCourseId))
                .Select(g => new { g.AssignedCourseId, g.Final })
                .ToListAsync();

            var totalGradeSlots = grades.Count;
            var gradedCount = grades.Count(g => g.Final != null);
            var gradedPct = totalGradeSlots == 0 ? 0m : (decimal)gradedCount / totalGradeSlots;

            // ===============================================
            // Build Dashboard Model
            // ===============================================
            var model = new TeacherDashboardViewModel
            {
                TeacherName = $"{user.FirstName} {user.LastName}",
                GradedPct = Math.Round(gradedPct, 4)
            };

            // Helper to build program grouping
            ProgramCourses BuildProgram(string programCode)
            {
                var ac = assigned
                    .Where(a => string.Equals(a.ProgramUi, programCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var items = ac
                    .Join(courses, a => a.CourseId, c => c.CourseId,
                        (a, c) => new { a.AssignedCourseId, c.CourseCode, c.Description })
                    .GroupJoin(rosters, x => x.AssignedCourseId, r => r.AssignedCourseId,
                        (x, rset) => new CourseItem
                        {
                            Code = x.CourseCode,
                            Title = x.Description,
                            Students = rset.FirstOrDefault()?.Students ?? 0
                        })
                    .OrderByDescending(ci => ci.Students)
                    .ThenBy(ci => ci.Code)
                    .ToList();

                return new ProgramCourses
                {
                    Courses = items,
                    StudentsTotal = items.Sum(i => i.Students)
                };
            }

            // Build IT and CS blocks
            model.IT = BuildProgram("IT");
            model.CS = BuildProgram("CS");

            model.TotalCourses = model.IT.Courses.Count + model.CS.Courses.Count;
            model.TotalStudents = model.IT.StudentsTotal + model.CS.StudentsTotal;

            model.Summary = new ProgramSummary
            {
                IT_Courses = model.IT.Courses.Count,
                CS_Courses = model.CS.Courses.Count,
                IT_Students = model.IT.StudentsTotal,
                CS_Students = model.CS.StudentsTotal
            };

            return model;
        }

        // ===============================================
        // HELPER: Normalize Program Codes Safely
        // ===============================================
        private static string NormalizeProgram(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "CS";
            var s = raw.Trim().ToUpperInvariant();

            // Exact matches only
            if (s == "BSIT" || s == "IT") return "IT";
            if (s == "BSCS" || s == "CS") return "CS";

            // Edge handling: fallback defaults
            if (s.Contains("IT")) return "IT";
            if (s.Contains("CS")) return "CS";

            // Default fallback to CS (safer for visualization)
            return "CS";
        }
    }
}
