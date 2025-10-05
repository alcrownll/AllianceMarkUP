using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class TeacherCourseService : ITeacherCourseService
    {
        private readonly AsiBasecodeDBContext _ctx;
        private readonly IAssignedCourseRepository _assignedCourseRepository;
        private readonly IGradeRepository _gradeRepository;
        private readonly IClassScheduleRepository _classScheduleRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public TeacherCourseService(
            AsiBasecodeDBContext ctx,
            IAssignedCourseRepository assignedCourseRepository,
            IGradeRepository gradeRepository,
            IClassScheduleRepository classScheduleRepository,
            IStudentRepository studentRepository,
            IUnitOfWork unitOfWork)
        {
            _ctx = ctx;
            _assignedCourseRepository = assignedCourseRepository;
            _gradeRepository = gradeRepository;
            _classScheduleRepository = classScheduleRepository;
            _studentRepository = studentRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<TeacherCourseViewModel>> GetTeacherAssignedCoursesAsync(int teacherId, string semester = null)
        {
            var currentSemester = semester ?? GetCurrentSemester();

            var assignedCourses = await _assignedCourseRepository.GetAssignedCourses()
                .Where(ac => ac.TeacherId == teacherId)
                .Where(ac => string.IsNullOrEmpty(semester) || ac.Semester == currentSemester)
                .Include(ac => ac.Course)
                .Include(ac => ac.ClassSchedules)
                .Include(ac => ac.Grades).ThenInclude(g => g.Student)
                .AsNoTracking()
                .ToListAsync();

            var result = new List<TeacherCourseViewModel>();

            foreach (var ac in assignedCourses)
            {
                var schedules = ac.ClassSchedules?.ToList() ?? new List<ClassSchedule>();
                var studentCount = ac.Grades?.Select(g => g.StudentId).Distinct().Count() ?? 0;

                result.Add(new TeacherCourseViewModel
                {
                    AssignedCourseId = ac.AssignedCourseId,
                    EDPCode = ac.EDPCode,
                    Subject = ac.Course?.CourseCode ?? "",
                    Description = ac.Course?.Description ?? "",
                    Type = ac.Type,
                    Units = ac.Units > 0 ? ac.Units : (ac.Course?.LecUnits + ac.Course?.LabUnits ?? 0),
                    DateTime = FormatSchedule(schedules),
                    Room = schedules.FirstOrDefault()?.Room ?? "",
                    Section = DetermineSection(ac.Program),
                    Course = ac.Program,
                    Semester = ac.Semester,
                    StudentCount = studentCount
                });
            }

            return result.OrderBy(tc => tc.Subject).ThenBy(tc => tc.Type).ToList();
        }

        public async Task<List<StudentGradeViewModel>> GetStudentsInCourseAsync(int assignedCourseId)
        {
            var grades = await _gradeRepository.GetGrades()
                .Where(g => g.AssignedCourseId == assignedCourseId)
                .Include(g => g.Student).ThenInclude(s => s.User).ThenInclude(u => u.UserProfile)
                .AsNoTracking()
                .ToListAsync();

            return grades.Select(g => new StudentGradeViewModel
            {
                StudentId = g.StudentId,
                GradeId = g.GradeId,
                AssignedCourseId = g.AssignedCourseId,
                IdNumber = g.Student?.User?.IdNumber ?? "",
                LastName = g.Student?.User?.LastName ?? "",
                FirstName = g.Student?.User?.FirstName ?? "",
                CourseYear = $"{g.Student?.Program} {g.Student?.YearLevel}",
                Gender = g.Student?.User?.UserProfile?.Gender ?? "",
                Prelims = g.Prelims,
                Midterm = g.Midterm,
                SemiFinal = g.SemiFinal,
                Final = g.Final,
                Remarks = g.Remarks ?? ""
            }).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
        }

        public async Task<List<TeacherClassScheduleViewModel>> GetTeacherClassSchedulesAsync(int teacherId, string semester = null)
        {
            var currentSemester = semester ?? GetCurrentSemester();

            var assignedCourses = await _assignedCourseRepository.GetAssignedCourses()
                .Where(ac => ac.TeacherId == teacherId)
                .Where(ac => string.IsNullOrEmpty(semester) || ac.Semester == currentSemester)
                .Include(ac => ac.Course)
                .Include(ac => ac.ClassSchedules)
                .AsNoTracking()
                .ToListAsync();

            var result = new List<TeacherClassScheduleViewModel>();

            foreach (var ac in assignedCourses)
            {
                var schedules = ac.ClassSchedules?.ToList() ?? new List<ClassSchedule>();
                var students = await GetStudentsInCourseAsync(ac.AssignedCourseId);

                result.Add(new TeacherClassScheduleViewModel
                {
                    AssignedCourseId = ac.AssignedCourseId,
                    EDPCode = ac.EDPCode,
                    Subject = ac.Course?.CourseCode ?? "",
                    Type = ac.Type,
                    Units = ac.Units > 0 ? ac.Units : (ac.Course?.LecUnits + ac.Course?.LabUnits ?? 0),
                    DateTime = FormatSchedule(schedules),
                    Room = schedules.FirstOrDefault()?.Room ?? "",
                    Section = DetermineSection(ac.Program),
                    Course = ac.Program,
                    Students = students
                });
            }

            return result.OrderBy(tc => tc.Subject).ThenBy(tc => tc.Type).ToList();
        }

        public async Task<List<StudentGradeViewModel>> GetStudentGradesForClassAsync(int assignedCourseId, string examType = null)
        {
            var students = await GetStudentsInCourseAsync(assignedCourseId);

            // Apply exam type filtering logic if needed
            if (!string.IsNullOrEmpty(examType))
            {
                foreach (var student in students)
                {
                    // Set read-only flags based on exam type
                    switch (examType.ToLower())
                    {
                        case "prelim":
                            student.IsReadOnly = false; // Prelim can be edited
                            break;
                        case "midterm":
                            student.IsReadOnly = student.Prelims == null; // Can edit if Prelim is entered
                            break;
                        case "semi-final":
                            student.IsReadOnly = student.Midterm == null; // Can edit if Midterm is entered
                            break;
                        case "final":
                            student.IsReadOnly = student.SemiFinal == null; // Can edit if Semi-final is entered
                            break;
                    }
                }
            }

            return students;
        }

        public async Task<bool> UpdateStudentGradesAsync(List<StudentGradeUpdateModel> grades)
        {
            try
            {
                foreach (var gradeUpdate in grades)
                {
                    var existingGrade = await _ctx.Grades
                        .FirstOrDefaultAsync(g => g.GradeId == gradeUpdate.GradeId);

                    if (existingGrade != null)
                    {
                        // Update only non-null values
                        if (gradeUpdate.Prelims.HasValue)
                            existingGrade.Prelims = gradeUpdate.Prelims;
                        if (gradeUpdate.Midterm.HasValue)
                            existingGrade.Midterm = gradeUpdate.Midterm;
                        if (gradeUpdate.SemiFinal.HasValue)
                            existingGrade.SemiFinal = gradeUpdate.SemiFinal;
                        if (gradeUpdate.Final.HasValue)
                            existingGrade.Final = gradeUpdate.Final;
                        if (!string.IsNullOrEmpty(gradeUpdate.Remarks))
                            existingGrade.Remarks = gradeUpdate.Remarks;

                        _gradeRepository.UpdateGrade(existingGrade);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<string>> GetTeacherProgramsAsync(int teacherId)
        {
            var programs = await _assignedCourseRepository.GetAssignedCourses()
                .Where(ac => ac.TeacherId == teacherId)
                .Select(ac => ac.Program)
                .Distinct()
                .Where(p => !string.IsNullOrEmpty(p))
                .ToListAsync();

            return programs.OrderBy(p => p).ToList();
        }

        public async Task<List<int>> GetTeacherYearLevelsAsync(int teacherId)
        {
            // Get year levels from students enrolled in teacher's courses
            var yearLevels = await _ctx.Students
                .Where(s => _ctx.Grades.Any(g => g.StudentId == s.StudentId && g.AssignedCourse.TeacherId == teacherId))
                .Where(s => !string.IsNullOrEmpty(s.YearLevel))
                .Select(s => s.YearLevel)
                .Distinct()
                .ToListAsync();

            // Convert string year levels to integers, handling various formats
            var intYearLevels = new List<int>();
            foreach (var yearLevel in yearLevels)
            {
                // Try to parse the year level, handling cases like "1", "2", "3", "4"
                if (int.TryParse(yearLevel.Trim(), out int parsedYear))
                {
                    intYearLevels.Add(parsedYear);
                }
                else
                {
                    // Handle cases like "1st", "2nd", "3rd", "4th"
                    var numericPart = new string(yearLevel.Where(char.IsDigit).ToArray());
                    if (int.TryParse(numericPart, out int extractedYear))
                    {
                        intYearLevels.Add(extractedYear);
                    }
                }
            }

            return intYearLevels.Distinct().OrderBy(y => y).ToList();
        }

        public async Task<List<StudentGradeViewModel>> SearchStudentsAsync(int teacherId, string searchName = null, 
            string searchId = null, string program = null, int? yearLevel = null)
        {
            // Get all grades for students in teacher's courses
            var gradesQuery = _gradeRepository.GetGrades()
                .Where(g => g.AssignedCourse.TeacherId == teacherId)
                .Include(g => g.Student).ThenInclude(s => s.User).ThenInclude(u => u.UserProfile)
                .AsQueryable();

            // Apply database-level filters first
            if (!string.IsNullOrEmpty(searchName))
            {
                gradesQuery = gradesQuery.Where(g => 
                    g.Student.User.FirstName.Contains(searchName) || 
                    g.Student.User.LastName.Contains(searchName));
            }

            if (!string.IsNullOrEmpty(searchId))
            {
                gradesQuery = gradesQuery.Where(g => g.Student.User.IdNumber.Contains(searchId));
            }

            if (!string.IsNullOrEmpty(program))
            {
                gradesQuery = gradesQuery.Where(g => g.Student.Program == program);
            }

            // Execute query and get results (apply year level filter in memory for flexibility)
            var allGrades = await gradesQuery.AsNoTracking().ToListAsync();
            
            // Apply year level filter in memory for more flexible matching
            if (yearLevel.HasValue)
            {
                allGrades = allGrades.Where(g => IsYearLevelMatch(g.Student.YearLevel, yearLevel.Value)).ToList();
            }

            // Group by student ID to eliminate duplicates and consolidate grades
            var studentGroups = allGrades
                .GroupBy(g => g.StudentId)
                .ToList();

            var result = new List<StudentGradeViewModel>();

            foreach (var group in studentGroups)
            {
                var student = group.First().Student;
                
                // Find the most complete grade record for this student
                var bestGrade = group
                    .OrderByDescending(g => (g.Prelims.HasValue ? 1 : 0) + 
                                           (g.Midterm.HasValue ? 1 : 0) + 
                                           (g.SemiFinal.HasValue ? 1 : 0) + 
                                           (g.Final.HasValue ? 1 : 0))
                    .ThenByDescending(g => g.GradeId)
                    .First();

                result.Add(new StudentGradeViewModel
                {
                    StudentId = student.StudentId,
                    GradeId = bestGrade.GradeId,
                    AssignedCourseId = bestGrade.AssignedCourseId,
                    IdNumber = student?.User?.IdNumber ?? "",
                    LastName = student?.User?.LastName ?? "",
                    FirstName = student?.User?.FirstName ?? "",
                    CourseYear = $"{student?.Program} {student?.YearLevel}",
                    Gender = student?.User?.UserProfile?.Gender ?? "",
                    Prelims = bestGrade.Prelims,
                    Midterm = bestGrade.Midterm,
                    SemiFinal = bestGrade.SemiFinal,
                    Final = bestGrade.Final,
                    Remarks = bestGrade.Remarks ?? ""
                });
            }

            return result.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
        }



        #region Private Helper Methods

        private bool IsYearLevelMatch(string dbYearLevel, int selectedYearLevel)
        {
            if (string.IsNullOrWhiteSpace(dbYearLevel))
                return false;

            var normalizedYearLevel = dbYearLevel.Trim().ToLowerInvariant();
            
            // Handle various year level formats
            var yearLevelPatterns = new[]
            {
                $"{selectedYearLevel}st",
                $"{selectedYearLevel}nd", 
                $"{selectedYearLevel}rd",
                $"{selectedYearLevel}th",
                $"year {selectedYearLevel}",
                $"{selectedYearLevel}",
                selectedYearLevel.ToString()
            };

            return yearLevelPatterns.Any(pattern => 
                normalizedYearLevel.Contains(pattern.ToLowerInvariant()) ||
                normalizedYearLevel == pattern.ToLowerInvariant());
        }

        private string GetCurrentSemester()
        {
            var now = DateTime.UtcNow;
            int syStart = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{syStart}-{syStart + 1}-1"; // Default to First Semester
        }

        private string FormatSchedule(List<ClassSchedule> schedules)
        {
            if (schedules == null || schedules.Count == 0) return "";

            var groups = schedules
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

        private string AbbrevDay(DayOfWeek day)
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

        private string To12h(TimeSpan t)
        {
            var dt = DateTime.Today.Add(t);
            return dt.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        private string DetermineSection(string program)
        {
            // Simple logic to determine section - can be enhanced
            return program switch
            {
                "BSCS" => "4A",
                "BSIT" => "4B", 
                "BSECE" => "4C",
                _ => "1A"
            };
        }

        #endregion
    }
}