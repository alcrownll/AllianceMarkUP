using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class AdminReportsService
    {
        private readonly ApplicationDbContext _context;

        public AdminReportsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<StudentOptionDto>> GetStudentOptionsAsync()
        {
            return await _context.Students
                .Include(s => s.Program)
                .AsNoTracking()
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Select(s => new StudentOptionDto
                {
                    StudentId = s.StudentId,
                    Name = string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
                    Program = s.Program != null ? s.Program.Name : string.Empty
                })
                .ToListAsync();
        }

        public async Task<IList<TeacherDirectoryItemDto>> GetTeacherDirectoryAsync()
        {
            return await _context.Teachers
                .AsNoTracking()
                .OrderBy(t => t.LastName)
                .ThenBy(t => t.FirstName)
                .Select(t => new TeacherDirectoryItemDto
                {
                    TeacherId = t.TeacherId,
                    Name = string.Join(" ", new[] { t.FirstName, t.MiddleName, t.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
                    Department = t.Department
                })
                .ToListAsync();
        }

        public async Task<StudentReportDto?> GetStudentReportAsync(int studentId)
        {
            var student = await _context.Students
                .Include(s => s.Program)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                return null;
            }

            var enrollments = await _context.Enrollments
                .Where(e => e.StudentId == studentId)
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.Class)
                    .ThenInclude(c => c.Section)
                .Include(e => e.Grade)
                .AsNoTracking()
                .ToListAsync();

            var gradeBreakdown = enrollments.Select(e =>
            {
                var course = e.Class?.Course;
                var finalGrade = ComputeAverageGrade(e.Grade);
                return new GradeBreakdownDto
                {
                    EnrollmentId = e.EnrollmentId,
                    CourseId = course?.CourseId ?? 0,
                    CourseCode = course?.Code ?? string.Empty,
                    CourseTitle = course?.Title ?? string.Empty,
                    Units = course?.Units ?? 0,
                    Prelim = e.Grade?.Prelim,
                    Midterm = e.Grade?.Midterm,
                    Prefinal = e.Grade?.Prefinal,
                    Final = e.Grade?.Final,
                    FinalGrade = finalGrade,
                    Status = finalGrade.HasValue && finalGrade.Value >= 75m ? "Passed" : "In Progress"
                };
            }).ToList();

            var courseGrades = gradeBreakdown
                .GroupBy(g => new { g.CourseId, g.CourseCode, g.CourseTitle })
                .Select(g => new CourseGradeDto
                {
                    CourseId = g.Key.CourseId,
                    CourseCode = g.Key.CourseCode,
                    CourseTitle = g.Key.CourseTitle,
                    AverageGrade = SafeAverage(g.Select(x => x.FinalGrade))
                })
                .Where(g => g.AverageGrade.HasValue)
                .ToList();

            var workloadMatrix = gradeBreakdown
                .Select(g => new WorkloadDto
                {
                    CourseId = g.CourseId,
                    CourseCode = g.CourseCode,
                    CourseTitle = g.CourseTitle,
                    Units = g.Units,
                    FinalGrade = g.FinalGrade
                })
                .ToList();

            var requiredUnits = student.Program?.RequiredUnits ?? 0;
            var earnedUnits = gradeBreakdown
                .Where(g => g.FinalGrade.HasValue && g.FinalGrade.Value >= 75m)
                .Sum(g => g.Units);
            var remainingUnits = requiredUnits > earnedUnits ? requiredUnits - earnedUnits : 0;
            var completionPercent = requiredUnits > 0 ? Math.Round((decimal)earnedUnits / requiredUnits * 100m, 2) : 0m;

            var unitProgress = new UnitProgressDto
            {
                CompletedUnits = earnedUnits,
                RemainingUnits = remainingUnits,
                RequiredUnits = requiredUnits,
                CompletionPercent = completionPercent
            };

            return new StudentReportDto
            {
                StudentId = student.StudentId,
                Name = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
                Program = student.Program?.Name ?? string.Empty,
                CourseGrades = courseGrades,
                WorkloadMatrix = workloadMatrix,
                UnitProgress = unitProgress,
                GradeBreakdown = gradeBreakdown
            };
        }

        public async Task<TeacherReportDto?> GetTeacherReportAsync(int teacherId)
        {
            var teacher = await _context.Teachers
                .Include(t => t.Classes)
                    .ThenInclude(c => c.Course)
                .Include(t => t.Classes)
                    .ThenInclude(c => c.Section)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                return null;
            }

            var classes = teacher.Classes?.ToList() ?? new List<Class>();
            var classIds = classes.Select(c => c.ClassId).ToList();

            var enrollments = await _context.Enrollments
                .Where(e => classIds.Contains(e.ClassId))
                .Include(e => e.Class)
                    .ThenInclude(c => c.Course)
                .Include(e => e.Class)
                    .ThenInclude(c => c.Section)
                .Include(e => e.Grade)
                .AsNoTracking()
                .ToListAsync();

            var submissions = await _context.Submissions
                .Where(s => classIds.Contains(s.ClassId))
                .AsNoTracking()
                .ToListAsync();

            var teachingLoadUnits = classes.Sum(c => c.Course?.Units ?? 0);
            var sectionCount = classes
                .Select(c => c.Section?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var allFinalGrades = enrollments
                .Select(e => ComputeAverageGrade(e.Grade))
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToList();

            var passedGrades = allFinalGrades.Count(g => g >= 75m);
            var passRatePercent = allFinalGrades.Count > 0 ? Math.Round((decimal)passedGrades / allFinalGrades.Count * 100m, 2) : 0m;

            var finalizedSubmissions = submissions.Count(s => IsSubmittedStatus(s.Status));
            var submissionPercent = submissions.Count > 0 ? Math.Round((decimal)finalizedSubmissions / submissions.Count * 100m, 2) : 0m;

            var overview = new TeacherOverviewDto
            {
                TeacherId = teacher.TeacherId,
                TeacherName = string.Join(" ", new[] { teacher.FirstName, teacher.MiddleName, teacher.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
                Department = teacher.Department,
                TeachingLoadUnits = teachingLoadUnits,
                SectionCount = sectionCount,
                PassRatePercent = passRatePercent,
                SubmissionsFinalizedPercent = submissionPercent
            };

            var coursePassRates = enrollments
                .GroupBy(e => new { e.Class?.Course?.CourseId, e.Class?.Course?.Code, e.Class?.Course?.Title })
                .Where(g => g.Key.CourseId.HasValue)
                .Select(g =>
                {
                    var grades = g.Select(e => ComputeAverageGrade(e.Grade)).Where(x => x.HasValue).Select(x => x.Value).ToList();
                    var passed = grades.Count(x => x >= 75m);
                    var failed = grades.Count(x => x < 75m);
                    var total = grades.Count;
                    var passRate = total > 0 ? Math.Round((decimal)passed / total * 100m, 2) : 0m;
                    return new CoursePassRateDto
                    {
                        CourseId = g.Key.CourseId!.Value,
                        CourseCode = g.Key.Code ?? string.Empty,
                        CourseTitle = g.Key.Title ?? string.Empty,
                        PassRatePercent = passRate,
                        PassedCount = passed,
                        FailedCount = failed
                    };
                })
                .ToList();

            var submissionProgress = new SubmissionProgressDto
            {
                SubmittedCount = finalizedSubmissions,
                PendingCount = submissions.Count - finalizedSubmissions,
                PercentComplete = submissionPercent
            };

            var enrollmentCounts = enrollments
                .GroupBy(e => e.ClassId)
                .ToDictionary(g => g.Key, g => g.Count());

            var assignments = classes
                .Select(c => new AssignmentDto
                {
                    ClassId = c.ClassId,
                    CourseCode = c.Course?.Code ?? string.Empty,
                    CourseTitle = c.Course?.Title ?? string.Empty,
                    Section = c.Section?.Name ?? string.Empty,
                    Schedule = c.Schedule ?? string.Empty,
                    Units = c.Course?.Units ?? 0,
                    EnrolledCount = enrollmentCounts.TryGetValue(c.ClassId, out var count) ? count : 0
                })
                .ToList();

            var submissionStatuses = submissions
                .Select(s =>
                {
                    var relatedClass = classes.FirstOrDefault(c => c.ClassId == s.ClassId);
                    return new SubmissionStatusDto
                    {
                        ClassId = s.ClassId,
                        CourseCode = relatedClass?.Course?.Code ?? string.Empty,
                        Section = relatedClass?.Section?.Name ?? string.Empty,
                        Status = s.Status ?? string.Empty,
                        SubmittedOn = s.SubmittedOn,
                        DueDate = s.DueDate
                    };
                })
                .OrderByDescending(s => s.SubmittedOn ?? s.DueDate)
                .ToList();

            return new TeacherReportDto
            {
                TeacherId = teacher.TeacherId,
                Overview = overview,
                CoursePassRates = coursePassRates,
                SubmissionProgress = submissionProgress,
                Assignments = assignments,
                SubmissionStatuses = submissionStatuses
            };
        }

        private static decimal? ComputeAverageGrade(Grade? grade)
        {
            if (grade == null)
            {
                return null;
            }

            var values = new List<decimal?>
            {
                grade.Prelim,
                grade.Midterm,
                grade.Prefinal,
                grade.Final,
                grade.FinalGrade
            };

            var numeric = values.Where(v => v.HasValue).Select(v => v.Value).ToList();
            if (!numeric.Any())
            {
                return null;
            }

            return Math.Round(numeric.Average(), 2);
        }

        private static decimal? SafeAverage(IEnumerable<decimal?> values)
        {
            var numeric = values.Where(v => v.HasValue).Select(v => v.Value).ToList();
            if (!numeric.Any())
            {
                return null;
            }

            return Math.Round(numeric.Average(), 2);
        }

        private static bool IsSubmittedStatus(string? status)
        {
            return string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Finalized", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
        }
    }
}

namespace ASI.Basecode.Services.ServiceModels
{
    public class StudentOptionDto
    {
        public int StudentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
    }

    public class TeacherDirectoryItemDto
    {
        public int TeacherId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    public class CourseGradeDto
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public decimal? AverageGrade { get; set; }
    }

    public class WorkloadDto
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public int Units { get; set; }
        public decimal? FinalGrade { get; set; }
    }

    public class UnitProgressDto
    {
        public int CompletedUnits { get; set; }
        public int RemainingUnits { get; set; }
        public int RequiredUnits { get; set; }
        public decimal CompletionPercent { get; set; }
    }

    public class GradeBreakdownDto
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public int Units { get; set; }
        public decimal? Prelim { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? Prefinal { get; set; }
        public decimal? Final { get; set; }
        public decimal? FinalGrade { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class StudentReportDto
    {
        public int StudentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public IList<CourseGradeDto> CourseGrades { get; set; } = new List<CourseGradeDto>();
        public IList<WorkloadDto> WorkloadMatrix { get; set; } = new List<WorkloadDto>();
        public UnitProgressDto UnitProgress { get; set; } = new UnitProgressDto();
        public IList<GradeBreakdownDto> GradeBreakdown { get; set; } = new List<GradeBreakdownDto>();
    }

    public class TeacherOverviewDto
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int TeachingLoadUnits { get; set; }
        public int SectionCount { get; set; }
        public decimal PassRatePercent { get; set; }
        public decimal SubmissionsFinalizedPercent { get; set; }
    }

    public class CoursePassRateDto
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public decimal PassRatePercent { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public class SubmissionProgressDto
    {
        public int SubmittedCount { get; set; }
        public int PendingCount { get; set; }
        public decimal PercentComplete { get; set; }
    }

    public class AssignmentDto
    {
        public int ClassId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Schedule { get; set; } = string.Empty;
        public int Units { get; set; }
        public int EnrolledCount { get; set; }
    }

    public class SubmissionStatusDto
    {
        public int ClassId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? SubmittedOn { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class TeacherReportDto
    {
        public int TeacherId { get; set; }
        public TeacherOverviewDto Overview { get; set; } = new TeacherOverviewDto();
        public IList<CoursePassRateDto> CoursePassRates { get; set; } = new List<CoursePassRateDto>();
        public SubmissionProgressDto SubmissionProgress { get; set; } = new SubmissionProgressDto();
        public IList<AssignmentDto> Assignments { get; set; } = new List<AssignmentDto>();
        public IList<SubmissionStatusDto> SubmissionStatuses { get; set; } = new List<SubmissionStatusDto>();
    }
}

namespace ASI.Basecode.Data.Models
{
    public partial class Student
    {
        public int StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int ProgramId { get; set; }
        public Program? Program { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }

    public partial class Program
    {
        public int ProgramId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int RequiredUnits { get; set; }
        public ICollection<Student> Students { get; set; } = new List<Student>();
    }

    public partial class Teacher
    {
        public int TeacherId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public ICollection<Class> Classes { get; set; } = new List<Class>();
    }

    public partial class Course
    {
        public int CourseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Units { get; set; }
        public ICollection<Class> Classes { get; set; } = new List<Class>();
    }

    public partial class Section
    {
        public int SectionId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public partial class Class
    {
        public int ClassId { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        public int TeacherId { get; set; }
        public Teacher? Teacher { get; set; }
        public int SectionId { get; set; }
        public Section? Section { get; set; }
        public string? Schedule { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }

    public partial class Enrollment
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public Student? Student { get; set; }
        public int ClassId { get; set; }
        public Class? Class { get; set; }
        public Grade? Grade { get; set; }
    }

    public partial class Grade
    {
        public int GradeId { get; set; }
        public int EnrollmentId { get; set; }
        public Enrollment? Enrollment { get; set; }
        public decimal? Prelim { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? Prefinal { get; set; }
        public decimal? Final { get; set; }
        public decimal? FinalGrade { get; set; }
    }

    public partial class Submission
    {
        public int SubmissionId { get; set; }
        public int ClassId { get; set; }
        public Class? Class { get; set; }
        public string? Status { get; set; }
        public DateTime? SubmittedOn { get; set; }
        public DateTime? DueDate { get; set; }
    }
}

namespace ASI.Basecode.Data
{
    public partial class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public virtual DbSet<Student> Students => Set<Student>();
        public virtual DbSet<Teacher> Teachers => Set<Teacher>();
        public virtual DbSet<Program> Programs => Set<Program>();
        public virtual DbSet<Course> Courses => Set<Course>();
        public virtual DbSet<Class> Classes => Set<Class>();
        public virtual DbSet<Section> Sections => Set<Section>();
        public virtual DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public virtual DbSet<Grade> Grades => Set<Grade>();
        public virtual DbSet<Submission> Submissions => Set<Submission>();
    }
}
