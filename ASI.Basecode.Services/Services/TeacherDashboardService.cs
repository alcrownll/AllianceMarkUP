using System;
using System.Collections.Generic;
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
                    Summary = new List<ProgramSummaryItem>()
                };

            // ===============================================
            // Fetch all assigned courses for this teacher
            // NOTE: Program now comes from FK -> Program.ProgramCode
            // ===============================================
            var assignedRaw = await _assignedRepo.GetAssignedCourses()
                .AsNoTracking()
                .Where(a => a.TeacherId == teacherId)
                .Select(a => new
                {
                    a.AssignedCourseId,
                    a.CourseId,
                    ProgramCode = a.Program != null ? a.Program.ProgramCode : null
                })
                .ToListAsync();

            if (assignedRaw.Count == 0)
                return new TeacherDashboardViewModel
                {
                    TeacherName = $"{user.FirstName} {user.LastName}",
                    TotalCourses = 0,
                    TotalStudents = 0,
                    GradedPct = 0,
                    Summary = new List<ProgramSummaryItem>()
                };

            // Keep original program codes instead of normalizing
            var assigned = assignedRaw.Select(a => new
            {
                a.AssignedCourseId,
                a.CourseId,
                ProgramCode = string.IsNullOrWhiteSpace(a.ProgramCode) ? "Unknown" : a.ProgramCode.Trim()
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
            
            // Get all unique programs from assigned courses
            var allPrograms = assigned.Select(a => a.ProgramCode).Distinct().ToList();
            
            // Helper to build program data
            async Task<ProgramData> BuildProgramDataAsync(string programCode)
            {
                var ac = assigned
                    .Where(a => string.Equals(a.ProgramCode, programCode, StringComparison.OrdinalIgnoreCase))
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

                // Calculate unique students for this program (avoid double counting within program)
                var programAssignedIds = ac.Select(a => a.AssignedCourseId).ToList();
                var uniqueStudentsCount = programAssignedIds.Count > 0
                    ? await _gradeRepo.GetGrades()
                        .AsNoTracking()
                        .Where(g => programAssignedIds.Contains(g.AssignedCourseId))
                        .Select(g => g.StudentId)
                        .Distinct()
                        .CountAsync()
                    : 0;

                return new ProgramData
                {
                    ProgramCode = programCode,
                    Courses = items,
                    StudentsTotal = uniqueStudentsCount  // Use unique student count instead of sum
                };
            }

            // Build all programs dynamically
            var programs = new List<ProgramData>();
            var summaryItems = new List<ProgramSummaryItem>();
            
            foreach (var programCode in allPrograms)
            {
                var programData = await BuildProgramDataAsync(programCode);
                programs.Add(programData);
                
                summaryItems.Add(new ProgramSummaryItem
                {
                    ProgramCode = programCode,
                    Courses = programData.Courses.Count,
                    Students = programData.StudentsTotal
                });
            }

            // Calculate total unique students across ALL assigned courses (to avoid double counting)
            var allUniqueStudents = await _gradeRepo.GetGrades()
                .AsNoTracking()
                .Where(g => assignedIds.Contains(g.AssignedCourseId))
                .Select(g => g.StudentId)
                .Distinct()
                .CountAsync();

            var model = new TeacherDashboardViewModel
            {
                TeacherName = $"{user.FirstName} {user.LastName}",
                GradedPct = Math.Round(gradedPct, 4),
                Programs = programs,
                Summary = summaryItems,
                TotalCourses = programs.Sum(p => p.Courses.Count),
                TotalStudents = allUniqueStudents  // Use unique student count instead of summing per course
            };

            return model;
        }
    }
}
