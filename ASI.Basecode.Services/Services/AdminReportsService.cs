using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Builds the data context behind the Admin Reports experience (directories, analytics,
    /// trends, filters). Controllers call this to retrieve report DTOs and directory listings.
    /// </summary>
    public class AdminReportsService : IAdminReportsService
    {
        private readonly AsiBasecodeDBContext _ctx;

        public AdminReportsService(AsiBasecodeDBContext ctx)
        {
            _ctx = ctx;
        }

        private static StaticSemesterDefinition ResolveSemesterDefinition(string termKey)
        {
            if (string.IsNullOrWhiteSpace(termKey))
            {
                return StaticSemesterDefinitions.First();
            }

            return StaticSemesterDefinitions.FirstOrDefault(def => def.Key.Equals(termKey, StringComparison.OrdinalIgnoreCase))
                ?? StaticSemesterDefinitions.First();
        }

        private static string NormalizeStaticTermKey(string termKey)
        {
            return ResolveSemesterDefinition(termKey).Key;
        }

        private static int? ResolveSemesterOrder(string termKey)
        {
            var definition = ResolveSemesterDefinition(termKey);
            return definition.Order > 0 ? definition.Order : (int?)null;
        }

        private static IList<ReportTermOptionModel> BuildStaticTermOptions(string schoolYear)
        {
            return StaticSemesterDefinitions
                .Select(def => new ReportTermOptionModel
                {
                    TermKey = def.Key,
                    Label = def.Label,
                    SchoolYear = schoolYear,
                    SemesterOrder = def.Order
                })
                .ToList();
        }

        private static bool IsAllSemester(string termKey)
        {
            return string.IsNullOrWhiteSpace(termKey) || termKey.Equals(StaticSemesterDefinitions[0].Key, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildContextLabel(string scope, string schoolYear, string termKey, string extra = null)
        {
            var segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(scope))
            {
                segments.Add(scope);
            }

            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                segments.Add(schoolYear);
            }

            if (!IsAllSemester(termKey))
            {
                segments.Add(ResolveSemesterDefinition(termKey).Label);
            }

            if (!string.IsNullOrWhiteSpace(extra))
            {
                segments.Add(extra);
            }

            return string.Join(" · ", segments);
        }

        public async Task<ReportsDashboardModel> GetDashboardAsync(
            string schoolYear = null,
            string termKey = null,
            int? highlightedTeacherId = null,
            int? highlightedStudentId = null,
            int? studentProgramId = null,
            int? studentCourseId = null)
        {
            var normalizedSchoolYear = await ResolveSchoolYearAsync(schoolYear);
            var termOptions = BuildStaticTermOptions(normalizedSchoolYear);
            var normalizedTermKey = NormalizeTermKey(termKey, termOptions);

            var studentDirectory = await LoadStudentDirectoryAsync(normalizedSchoolYear, normalizedTermKey, studentProgramId, studentCourseId);
            var studentCourses = await LoadCourseOptionsAsync(normalizedSchoolYear, normalizedTermKey, studentProgramId);
            var studentPrograms = await LoadStudentProgramOptionsAsync();
            var aggregateStudentAnalytics = await BuildStudentAggregateAnalyticsAsync(normalizedSchoolYear, normalizedTermKey, studentProgramId);

            StudentAnalyticsModel selectedStudentAnalytics = aggregateStudentAnalytics;
            int? resolvedStudentId = null;
            if (highlightedStudentId.HasValue && studentDirectory.Any(student => student.StudentId == highlightedStudentId.Value))
            {
                selectedStudentAnalytics = await GetStudentAnalyticsAsync(highlightedStudentId.Value, normalizedSchoolYear, normalizedTermKey);
                resolvedStudentId = highlightedStudentId.Value;
            }

            var teacherDirectory = await LoadTeacherDirectoryAsync(normalizedSchoolYear, normalizedTermKey);
            var aggregateTeacherDetail = await BuildTeacherAggregateDetailAsync(normalizedSchoolYear, normalizedTermKey);
            // default values for teacher detail
            aggregateTeacherDetail ??= new TeacherDetailModel
            {
                TeacherId = 0,
                Name = "Select Teacher",
                Department = "--",
                Email = "--",
                Rank = string.Empty,
                TeachingLoadUnits = 0,
                TeachingLoadCount = 0,
                SectionCount = 0,
                PassRatePercent = 0m,
                SubmissionCompletionPercent = 0m,
                Assignments = new List<TeacherAssignmentModel>(),
                CoursePassRates = new List<CoursePassRateModel>(),
                SubmissionStatuses = new List<TeacherSubmissionStatusModel>(),
                SubmissionSummary = new List<NamedValueModel>(),
                PassCount = 0,
                FailCount = 0,
                IncompleteCount = 0,
                IsAggregate = true,
                ContextLabel = BuildContextLabel("Select Teacher", normalizedSchoolYear, normalizedTermKey, null)
            };

            TeacherDetailModel selectedTeacherDetail = aggregateTeacherDetail;
            int? resolvedTeacherId = null;
            if (highlightedTeacherId.HasValue && teacherDirectory.Any(teacher => teacher.TeacherId == highlightedTeacherId.Value))
            {
                selectedTeacherDetail = await GetTeacherDetailAsync(highlightedTeacherId.Value, normalizedSchoolYear, normalizedTermKey);
                resolvedTeacherId = highlightedTeacherId.Value;
            }

            return new ReportsDashboardModel
            {
                SchoolYear = normalizedSchoolYear,
                TermKey = normalizedTermKey,
                AvailableSchoolYears = await GetAvailableSchoolYearsAsync(),
                TermOptions = termOptions,
                Overall = await BuildOverallAsync(normalizedSchoolYear, normalizedTermKey, studentProgramId),
                Student = new ReportsStudentModel
                {
                    Students = studentDirectory,
                    SelectedStudentId = resolvedStudentId,
                    Programs = studentPrograms,
                    SelectedProgramId = studentProgramId,
                    Analytics = selectedStudentAnalytics,
                    AggregateAnalytics = aggregateStudentAnalytics,
                    Courses = studentCourses,
                    SelectedCourseId = studentCourseId
                },
                Teacher = new ReportsTeacherModel
                {
                    Directory = teacherDirectory,
                    SelectedTeacher = selectedTeacherDetail,
                    AggregateDetail = aggregateTeacherDetail
                }
            };
        }

        public async Task<TeacherDetailModel> GetTeacherDetailAsync(int teacherId, string schoolYear = null, string termKey = null)
        {
            var normalizedSchoolYear = await ResolveSchoolYearAsync(schoolYear);
            var normalizedTermKey = NormalizeStaticTermKey(termKey);

            var teacher = await _ctx.Teachers
                .Include(t => t.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId
                    && t.User != null
                    && t.User.Role != null
                    && t.User.Role.ToLower() == "teacher");
        
            if (teacher == null)
            {
                return new TeacherDetailModel();
            }

            var assignedCoursesQuery = _ctx.AssignedCourses
                .Where(ac => ac.TeacherId == teacherId)
                .Include(ac => ac.Course)
                .Include(ac => ac.Program)
                .Include(ac => ac.ClassSchedules)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedSchoolYear))
            {
                assignedCoursesQuery = assignedCoursesQuery.Where(ac => ac.SchoolYear == normalizedSchoolYear);
            }

            var assignedCourses = await assignedCoursesQuery.ToListAsync();

            if (!assignedCourses.Any())
            {
                assignedCourses = await _ctx.AssignedCourses
                    .Where(ac => ac.TeacherId == teacherId)
                    .Include(ac => ac.Course)
                    .Include(ac => ac.Program)
                    .Include(ac => ac.ClassSchedules)
                    .AsNoTracking()
                    .ToListAsync();
            }

            if (!IsAllSemester(normalizedTermKey))
            {
                assignedCourses = assignedCourses
                    .Where(ac => MatchesTermKey(ac.Semester, normalizedTermKey))
                    .ToList();
            }

            var gradesQuery = _ctx.Grades
                .Where(g => g.AssignedCourse.TeacherId == teacherId)
                .Include(g => g.AssignedCourse)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedSchoolYear))
            {
                gradesQuery = gradesQuery.Where(g => g.AssignedCourse.SchoolYear == normalizedSchoolYear);
            }

            var grades = await gradesQuery.ToListAsync();

            if (!grades.Any())
            {
                grades = await _ctx.Grades
                    .Where(g => g.AssignedCourse.TeacherId == teacherId)
                    .Include(g => g.AssignedCourse)
                    .AsNoTracking()
                    .ToListAsync();
            }

            if (!IsAllSemester(normalizedTermKey))
            {
                grades = grades
                    .Where(g => MatchesTermKey(g.AssignedCourse.Semester, normalizedTermKey))
                    .ToList();
            }

            var assignments = assignedCourses.Select(ac =>
            {
                var courseGrades = grades
                    .Where(g => g.AssignedCourseId == ac.AssignedCourseId)
                    .ToList();

                var finalGradeValues = courseGrades
                    .Select(ComputeFinalGradeValue)
                    .Where(value => value.HasValue)
                    .Select(value => value.Value)
                    .ToList();

                var hasIncompleteGrades = courseGrades.Any(g => !IsGradeComplete(g));
                decimal? averageFinalGrade = finalGradeValues.Any()
                    ? Math.Round(finalGradeValues.Average(), 2)
                    : (decimal?)null;

                var status = hasIncompleteGrades
                    ? "Incomplete"
                    : MapFinalGradeToStatus(averageFinalGrade);

                return new TeacherAssignmentModel
                {
                    CourseCode = ac.Course?.CourseCode ?? ac.EDPCode,
                    SubjectName = ac.Course?.Description ?? ac.Course?.CourseCode ?? ac.EDPCode,
                    Schedule = BuildScheduleDescription(ac.ClassSchedules),
                    Units = ac.Units,
                    Enrolled = courseGrades.Count,
                    FinalGrade = averageFinalGrade,
                    Status = status
                };
            }).ToList();

            var coursePassRates = assignedCourses.Select(ac =>
            {
                var courseGrades = grades
                    .Where(g => g.AssignedCourseId == ac.AssignedCourseId)
                    .ToList();

                var completedGrades = courseGrades
                    .Select(ComputeFinalGradeValue)
                    .Where(value => value.HasValue)
                    .Select(value => value.Value)
                    .ToList();

                var passRate = completedGrades.Any()
                    ? Math.Round(completedGrades.Count(value => value < 3m) * 100m / completedGrades.Count, 1)
                    : 0m;

                return new CoursePassRateModel
                {
                    CourseCode = ac.Course?.CourseCode ?? ac.EDPCode,
                    SubjectName = ac.Course?.Description ?? ac.Course?.CourseCode ?? ac.EDPCode,
                    PassRatePercent = passRate
                };
            })
            .OrderByDescending(model => model.PassRatePercent)
            .ToList();

            var submissionStatuses = assignedCourses.Select(ac =>
            {
                var courseGrades = grades
                    .Where(g => g.AssignedCourseId == ac.AssignedCourseId)
                    .ToList();
                var isComplete = courseGrades.Any() && courseGrades.All(IsGradeComplete);

                return new TeacherSubmissionStatusModel
                {
                    CourseCode = ac.Course?.CourseCode ?? ac.EDPCode,
                    SubjectName = ac.Course?.Description ?? ac.Course?.CourseCode ?? ac.EDPCode,
                    Status = isComplete ? "Complete" : "Incomplete",
                    IsComplete = isComplete
                };
            }).ToList();

            var submissionSummary = submissionStatuses
                .GroupBy(status => status.IsComplete)
                .Select(group => new NamedValueModel
                {
                    Name = group.Key ? "All grades submitted" : "Some grades are ungraded",
                    Value = group.Count()
                })
                .ToList();

            var aggregateCompletedGrades = grades
                .Select(ComputeFinalGradeValue)
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToList();

            var passRateAggregate = aggregateCompletedGrades.Any()
                ? Math.Round(aggregateCompletedGrades.Count(value => value < 3m) * 100m / aggregateCompletedGrades.Count, 1)
                : 0m;

            var submissionCompletion = assignments.Count == 0
                ? 0m
                : Math.Round((decimal)submissionStatuses.Count(status => status.IsComplete) / assignments.Count * 100m, 1);

            var passFailCounts = assignments.Aggregate(new PassFailAccumulator(), (acc, assignment) =>
            {
                switch ((assignment.Status ?? "").ToLowerInvariant())
                {
                    case "passed":
                        acc.Pass += 1;
                        break;
                    case "failed":
                        acc.Fail += 1;
                        break;
                    default:
                        acc.Incomplete += 1;
                        break;
                }

                return acc;
            });

            return new TeacherDetailModel
            {
                TeacherId = teacher.TeacherId,
                Name = teacher.User != null ? $"{teacher.User.FirstName} {teacher.User.LastName}" : "Teacher",
                Department = ResolveTeacherDepartment(assignedCourses, teacher),
                Email = teacher.User?.Email ?? "--",
                Rank = teacher.Position,
                TeachingLoadUnits = assignedCourses.Sum(ac => ac.Units),
                TeachingLoadCount = assignments.Count,
                SectionCount = assignments.Count,
                PassRatePercent = passRateAggregate,
                SubmissionCompletionPercent = submissionCompletion,
                Assignments = assignments,
                CoursePassRates = coursePassRates,
                SubmissionStatuses = submissionStatuses,
                SubmissionSummary = submissionSummary,
                PassCount = passFailCounts.Pass,
                FailCount = passFailCounts.Fail,
                IncompleteCount = passFailCounts.Incomplete,
                IsAggregate = false,
                ContextLabel = BuildContextLabel(teacher.User != null ? $"{teacher.User.FirstName} {teacher.User.LastName}" : "Teacher", normalizedSchoolYear, normalizedTermKey,
                    null)
            };
        }

        public async Task<StudentAnalyticsModel> GetStudentAnalyticsAsync(int studentId, string schoolYear = null, string termKey = null)
        {
            var normalizedSchoolYear = await ResolveSchoolYearAsync(schoolYear);
            var termOptions = await BuildTermOptionsAsync(normalizedSchoolYear);
            var normalizedTermKey = NormalizeTermKey(termKey, termOptions);

            var gradesQuery = _ctx.Grades
                .Where(g => g.StudentId == studentId && g.AssignedCourse.SchoolYear == normalizedSchoolYear)
                .Include(g => g.AssignedCourse)
                .ThenInclude(ac => ac.Course)
                .Include(g => g.AssignedCourse)
                .ThenInclude(ac => ac.Program)
                .AsNoTracking();

            var grades = await gradesQuery.ToListAsync();

            if (!string.IsNullOrEmpty(normalizedTermKey))
            {
                grades = grades
                    .Where(g => MatchesTermKey(g.AssignedCourse.Semester, normalizedTermKey))
                    .ToList();
            }

            var courseGrades = grades
                .GroupBy(g => g.AssignedCourse.Course.CourseCode ?? g.AssignedCourse.EDPCode)
                .Select(group =>
                {
                    var forcedGrades = group.Select(ForceFinalGradeValue).ToList();
                    var average = forcedGrades.Any()
                        ? Math.Round(forcedGrades.Average(), 2)
                        : 5m;

                    return new StudentCourseGradeModel
                    {
                        CourseCode = group.Key,
                        Grade = average
                    };
                })
                .OrderBy(g => g.CourseCode)
                .ToList();

            var gradeBreakdown = grades.Select(g =>
            {
                var forcedFinal = ForceFinalGradeValue(g);
                return new StudentGradeBreakdownModel
                {
                    EdpCode = g.AssignedCourse.EDPCode,
                    Subject = g.AssignedCourse.Course.Description,
                    Prelim = g.Prelims,
                    Midterm = g.Midterm,
                    Prefinal = g.SemiFinal,
                    Final = g.Final,
                    FinalGrade = forcedFinal,
                    Status = MapFinalGradeToStatus(forcedFinal)
                };
            }).ToList();

            var statusMix = gradeBreakdown
                .GroupBy(row => row.Status ?? "Incomplete")
                .Select(group => new NamedValueModel
                {
                    Name = group.Key,
                    Value = group.Count()
                })
                .ToList();

            var volatility = CalculateVolatility(courseGrades.Select(cg => cg.Grade).ToList());
            var consistencyLabel = MapVolatilityToLabel(volatility);

            var comparativeHighlights = BuildComparativeHighlights(grades);

            return new StudentAnalyticsModel
            {
                CourseGrades = courseGrades,
                GradeBreakdown = gradeBreakdown,
                StatusMix = statusMix,
                ConsistencyLabel = consistencyLabel,
                ComparativeHighlights = comparativeHighlights,
                IsAggregate = false,
                ContextLabel = BuildContextLabel(null, normalizedSchoolYear, normalizedTermKey, await ResolveStudentNameAsync(studentId))
            };
        }

        private async Task<string> ResolveSchoolYearAsync(string schoolYear)
        {
            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                return schoolYear;
            }

            try
            {
                var mostRecent = await _ctx.AssignedCourses
                    .Where(ac => ac.SchoolYear != null)
                    .OrderByDescending(ac => ac.SchoolYear)
                    .Select(ac => ac.SchoolYear)
                    .FirstOrDefaultAsync();

                return mostRecent ?? string.Empty;
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
        }

        private async Task<IList<string>> GetAvailableSchoolYearsAsync()
        {
            return await _ctx.AssignedCourses
                .Where(ac => ac.SchoolYear != null)
                .Select(ac => ac.SchoolYear)
                .Distinct()
                .OrderBy(year => year)
                .ToListAsync();
        }

        private Task<IList<ReportTermOptionModel>> BuildTermOptionsAsync(string schoolYear)
        {
            var normalizedYear = string.IsNullOrWhiteSpace(schoolYear)
                ? string.Empty
                : schoolYear;

            var options = BuildStaticTermOptions(normalizedYear);
            return Task.FromResult<IList<ReportTermOptionModel>>(options);
        }

        private static string NormalizeTermKey(string termKey, IList<ReportTermOptionModel> options)
        {
            var resolved = ResolveSemesterDefinition(termKey).Key;

            if (options != null && options.Any(option => string.Equals(option.TermKey, resolved, StringComparison.OrdinalIgnoreCase)))
            {
                return resolved;
            }

            return resolved;
        }

        private async Task<IList<StudentOptionModel>> LoadStudentDirectoryAsync(string schoolYear, string termKey, int? programId, int? courseId)
        {
            var query = _ctx.Students
                .Include(s => s.User)
                .Include(s => s.Grades)
                    .ThenInclude(g => g.AssignedCourse)
                        .ThenInclude(ac => ac.Program)
                .AsNoTracking()
                .AsQueryable();

            if (programId.HasValue)
            {
                query = query.Where(s => s.Grades.Any(g => g.AssignedCourse != null && g.AssignedCourse.ProgramId == programId.Value));
            }

            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                query = query.Where(s => s.Grades.Any(g => g.AssignedCourse != null && g.AssignedCourse.SchoolYear == schoolYear));
            }

            var students = await query
                .OrderBy(s => s.User.LastName)
                .ThenBy(s => s.User.FirstName)
                .ToListAsync();

            if (!IsAllSemester(termKey))
            {
                students = students
                    .Where(student => student.Grades != null
                        && student.Grades.Any(grade => grade.AssignedCourse != null && MatchesTermKey(grade.AssignedCourse.Semester, termKey)))
                    .ToList();
            }

            if (courseId.HasValue)
            {
                students = students
                    .Where(student => student.Grades != null
                        && student.Grades.Any(grade => grade.AssignedCourse != null && grade.AssignedCourse.CourseId == courseId.Value))
                    .ToList();
            }

            return students
                .Select(student => new StudentOptionModel
                {
                    StudentId = student.StudentId,
                    Name = student.User != null ? $"{student.User.FirstName} {student.User.LastName}" : "Student",
                    ProgramId = student.Grades?
                        .Where(g => g.AssignedCourse?.ProgramId != null)
                        .Select(g => (int?)g.AssignedCourse.ProgramId)
                        .FirstOrDefault(),
                    Program = student.Grades?
                        .Where(g => g.AssignedCourse?.Program != null)
                        .Select(g => g.AssignedCourse.Program.ProgramName)
                        .FirstOrDefault()
                        ?? student.Program,
                    Sections = new List<string>()
                })
                .ToList();
        }

        private async Task<IList<StudentCourseOptionModel>> LoadCourseOptionsAsync(string schoolYear, string termKey, int? programId)
        {
            if (string.IsNullOrWhiteSpace(schoolYear))
            {
                return new List<StudentCourseOptionModel>();
            }

            var query = _ctx.AssignedCourses
                .Include(ac => ac.Course)
                .Include(ac => ac.Program)
                .AsNoTracking()
                .Where(ac => ac.SchoolYear == schoolYear);

            if (programId.HasValue)
            {
                query = query.Where(ac => ac.ProgramId == programId.Value);
            }

            var coursesQuery = await query
                .Select(ac => new
                {
                    ac.AssignedCourseId,
                    ac.CourseId,
                    ac.ProgramId,
                    ac.Course.CourseCode,
                    ac.Course.Description,
                    ac.SchoolYear,
                    TermKey = ac.Semester
                })
                .ToListAsync();

            if (!IsAllSemester(termKey))
            {
                coursesQuery = coursesQuery
                    .Where(course => MatchesTermKey(course.TermKey, termKey))
                    .ToList();
            }

            return coursesQuery
                .GroupBy(item => item.CourseId)
                .Select(group =>
                {
                    var first = group.First();
                    var termInfo = ParseTerm(first.TermKey);

                    return new StudentCourseOptionModel
                    {
                        AssignedCourseId = first.AssignedCourseId,
                        CourseId = first.CourseId,
                        ProgramId = first.ProgramId,
                        CourseCode = first.CourseCode,
                        CourseName = first.Description,
                        SchoolYear = first.SchoolYear,
                        TermKey = termInfo?.TermKey ?? string.Empty
                    };
                })
                .OrderBy(model => model.CourseCode ?? model.CourseName)
                .ToList();
        }

        private async Task<IList<ReportsProgramOptionModel>> LoadStudentProgramOptionsAsync()
        {
            return await _ctx.Programs
                .Where(program => program.IsActive)
                .OrderBy(program => program.ProgramName)
                .AsNoTracking()
                .Select(program => new ReportsProgramOptionModel
                {
                    ProgramId = program.ProgramId,
                    ProgramCode = program.ProgramCode,
                    ProgramName = program.ProgramName
                })
                .ToListAsync();
        }

        public async Task<IList<StudentOptionModel>> GetStudentDirectoryAsync(string schoolYear = null, string termKey = null, int? programId = null, int? courseId = null)
        {
            var normalizedSchoolYear = await ResolveSchoolYearAsync(schoolYear);
            var termOptions = await BuildTermOptionsAsync(normalizedSchoolYear);
            var normalizedTermKey = NormalizeTermKey(termKey, termOptions);

            var studentOptions = await LoadStudentDirectoryAsync(normalizedSchoolYear, normalizedTermKey, programId, courseId);

            if (courseId.HasValue)
            {
                var courseGrades = await _ctx.Grades
                    .Where(g => g.AssignedCourse.CourseId == courseId.Value
                        && g.AssignedCourse.SchoolYear == normalizedSchoolYear)
                    .Include(g => g.AssignedCourse)
                    .AsNoTracking()
                    .ToListAsync();

                if (!IsAllSemester(normalizedTermKey))
                {
                    courseGrades = courseGrades
                        .Where(g => g.AssignedCourse != null && MatchesTermKey(g.AssignedCourse.Semester, normalizedTermKey))
                        .ToList();
                }

                var enrolledIds = courseGrades
                    .Select(g => g.StudentId)
                    .Distinct()
                    .ToList();

                studentOptions = studentOptions
                    .Where(option => enrolledIds.Contains(option.StudentId))
                    .ToList();
            }

            return studentOptions;
        }

        private async Task<IList<TeacherDirectoryItemModel>> LoadTeacherDirectoryAsync(string schoolYear, string termKey)
        {
            var teacherUsers = await _ctx.Users
                .Include(u => u.Teacher)
                .Where(u => u.Teacher != null
                    && u.Role != null
                    && u.Role.ToLower() == "teacher")
                .AsNoTracking()
                .ToListAsync();

            var teacherIds = teacherUsers
                .Select(u => u.Teacher?.TeacherId)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            var assignmentsQuery = _ctx.AssignedCourses
                .Where(ac => teacherIds.Contains(ac.TeacherId));

            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                assignmentsQuery = assignmentsQuery.Where(ac => ac.SchoolYear == schoolYear);
            }

            var assignments = await assignmentsQuery
                .Include(ac => ac.Program)
                .AsNoTracking()
                .ToListAsync();
            var assignedTeacherIds = assignments
                .Select(ac => ac.TeacherId)
                .Where(id => id != 0)
                .Distinct()
                .ToHashSet();

            if (!assignedTeacherIds.Any())
            {
                return new List<TeacherDirectoryItemModel>();
            }
            var assignmentsForStats = assignments;

            if (!string.IsNullOrEmpty(termKey))
            {
                assignmentsForStats = assignments
                    .Where(ac => MatchesTermKey(ac.Semester, termKey))
                    .ToList();
            }

            var eligibleTeacherIds = assignedTeacherIds;

            var stats = assignmentsForStats
                .GroupBy(ac => ac.TeacherId)
                .ToDictionary(
                    group => group.Key,
                    group => new
                    {
                        LoadUnits = group.Sum(ac => ac.Units),
                        Sections = group.Count(),
                        Department = group.Select(ac => ac.Program?.ProgramName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    });

            var directory = teacherUsers
                .Select(user =>
                {
                    var teacher = user.Teacher;
                    if (teacher == null)
                    {
                        return null;
                    }

                    if (!eligibleTeacherIds.Contains(teacher.TeacherId))
                    {
                        return null;
                    }

                    stats.TryGetValue(teacher.TeacherId, out var stat);

                    return new TeacherDirectoryItemModel
                    {
                        TeacherId = teacher.TeacherId,
                        Name = $"{user.FirstName} {user.LastName}",
                        Department = stat?.Department ?? teacher.Position ?? "--",
                        Email = user.Email ?? "--",
                        Rank = teacher.Position,
                        LoadUnits = stat?.LoadUnits ?? 0,
                        Sections = stat?.Sections ?? 0
                    };
                })
                .Where(item => item != null)
                .OrderBy(item => item.Name)
                .ToList();

            return directory;
        }

        private async Task<ReportsOverallModel> BuildOverallAsync(string schoolYear, string termKey, int? programId)
        {
            var summary = await BuildOverallSummaryAsync(schoolYear, termKey, programId);
            var trend = await BuildEnrollmentTrendAsync(schoolYear, termKey, programId);

            return new ReportsOverallModel
            {
                Summary = summary,
                EnrollmentTrend = trend
            };
        }

        private async Task<ReportsOverallSummary> BuildOverallSummaryAsync(string schoolYear, string termKey, int? programId)
        {
            var students = await _ctx.Grades
                .Where(g => string.IsNullOrWhiteSpace(schoolYear) || g.AssignedCourse.SchoolYear == schoolYear)
                .Where(g => !programId.HasValue || g.AssignedCourse.ProgramId == programId.Value)
                .AsNoTracking()
                .ToListAsync();

            if (!IsAllSemester(termKey))
            {
                students = students
                    .Where(g => MatchesTermKey(g.AssignedCourse.Semester, termKey))
                    .ToList();
            }

            var distinctStudents = students.Select(g => g.StudentId).Distinct().Count();
            var gwaValues = students
                .Select(ComputeFinalGradeValue)
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToList();

            var passRatePercent = gwaValues.Any()
                ? Math.Round(gwaValues.Count(value => value < 3m) * 100m / gwaValues.Count, 1)
                : 0m;

            var averageGwa = gwaValues.Any()
                ? Math.Round(gwaValues.Average(), 2)
                : 0m;

            return new ReportsOverallSummary
            {
                TotalEnrolled = distinctStudents,
                AverageGwa = averageGwa,
                PassRatePercent = passRatePercent,
                GrowthPercent = 0m,
                RetentionPercent = 0m
            };
        }

        private async Task<IList<TrendPointModel>> BuildEnrollmentTrendAsync(string schoolYear, string termKey, int? programId)
        {
            var grades = await _ctx.Grades
                .Where(g => string.IsNullOrWhiteSpace(schoolYear) || g.AssignedCourse.SchoolYear == schoolYear)
                .Include(g => g.AssignedCourse)
                .AsNoTracking()
                .ToListAsync();

            var trend = grades
                .Where(g => g.AssignedCourse != null)
                .GroupBy(g => g.AssignedCourse.SchoolYear)
                .Select(group => new TrendPointModel
                {
                    Label = group.Key,
                    TermKey = group.Key,
                    Value = group.Select(g => g.StudentId).Distinct().Count()
                })
                .OrderBy(point => point.Label)
                .ToList();

            return trend;
        }

        private async Task<string> ResolveProgramLabelAsync(int programId)
        {
            var program = await _ctx.Programs
                .Where(p => p.ProgramId == programId)
                .Select(p => new { p.ProgramCode, p.ProgramName })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (program == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(program.ProgramCode))
            {
                return program.ProgramName;
            }

            if (string.IsNullOrWhiteSpace(program.ProgramName))
            {
                return program.ProgramCode;
            }

            return $"{program.ProgramCode} - {program.ProgramName}";
        }

        private async Task<string> ResolveStudentNameAsync(int studentId)
        {
            var student = await _ctx.Students
                .Include(s => s.User)
                .Where(s => s.StudentId == studentId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (student?.User == null)
            {
                return null;
            }

            return $"{student.User.FirstName} {student.User.LastName}";
        }

        private async Task<StudentAnalyticsModel> BuildStudentAggregateAnalyticsAsync(string schoolYear, string termKey, int? programId = null)
        {
            var grades = await _ctx.Grades
                .Where(g => string.IsNullOrWhiteSpace(schoolYear) || g.AssignedCourse.SchoolYear == schoolYear)
                .Where(g => !programId.HasValue || g.AssignedCourse.ProgramId == programId.Value)
                .Include(g => g.AssignedCourse)
                    .ThenInclude(ac => ac.Course)
                .AsNoTracking()
                .ToListAsync();

            if (!IsAllSemester(termKey))
            {
                grades = grades
                    .Where(g => MatchesTermKey(g.AssignedCourse.Semester, termKey))
                    .ToList();
            }

            var courseGrades = grades
                .Where(g => g.AssignedCourse?.Course != null)
                .GroupBy(g => g.AssignedCourse.Course.CourseCode ?? g.AssignedCourse.EDPCode)
                .Select(group =>
                {
                    var forcedGrades = group.Select(ForceFinalGradeValue).ToList();
                    var average = forcedGrades.Any() ? Math.Round(forcedGrades.Average(), 2) : 0m;

                    return new StudentCourseGradeModel
                    {
                        CourseCode = group.Key,
                        Grade = average
                    };
                })
                .OrderBy(model => model.CourseCode)
                .ToList();

            var gradeBreakdown = grades
                .Select(g => new StudentGradeBreakdownModel
                {
                    EdpCode = g.AssignedCourse.EDPCode,
                    Subject = g.AssignedCourse.Course.Description,
                    Prelim = g.Prelims,
                    Midterm = g.Midterm,
                    Prefinal = g.SemiFinal,
                    Final = g.Final,
                    FinalGrade = ForceFinalGradeValue(g),
                    Status = MapFinalGradeToStatus(ComputeFinalGradeValue(g))
                })
                .ToList();

            var statusMix = gradeBreakdown
                .GroupBy(row => row.Status ?? "Incomplete")
                .Select(group => new NamedValueModel
                {
                    Name = group.Key,
                    Value = group.Count()
                })
                .ToList();

            return new StudentAnalyticsModel
            {
                CourseGrades = courseGrades,
                GradeBreakdown = gradeBreakdown,
                StatusMix = statusMix,
                ConsistencyLabel = "",
                ComparativeHighlights = new List<StudentComparativeHighlightModel>(),
                IsAggregate = true,
                ContextLabel = BuildContextLabel("All Students", schoolYear, termKey)
            };
        }

        private async Task<TeacherDetailModel> BuildTeacherAggregateDetailAsync(string schoolYear, string termKey)
        {
            var assignments = await _ctx.AssignedCourses
                .Where(ac => string.IsNullOrWhiteSpace(schoolYear) || ac.SchoolYear == schoolYear)
                .Include(ac => ac.Course)
                .Include(ac => ac.Program)
                .AsNoTracking()
                .ToListAsync();

            if (!IsAllSemester(termKey))
            {
                assignments = assignments
                    .Where(ac => MatchesTermKey(ac.Semester, termKey))
                    .ToList();
            }

            var grades = await _ctx.Grades
                .Where(g => string.IsNullOrWhiteSpace(schoolYear) || g.AssignedCourse.SchoolYear == schoolYear)
                .Include(g => g.AssignedCourse)
                .AsNoTracking()
                .ToListAsync();

            if (!IsAllSemester(termKey))
            {
                grades = grades
                    .Where(g => MatchesTermKey(g.AssignedCourse.Semester, termKey))
                    .ToList();
            }

            var teacherAssignments = assignments
                .GroupBy(ac => ac.TeacherId)
                .Select(group => new TeacherAssignmentModel
                {
                    CourseCode = group.First().Course?.CourseCode ?? group.First().EDPCode,
                    SubjectName = group.First().Course?.Description ?? group.First().EDPCode,
                    Schedule = "--",
                    Units = group.Sum(ac => ac.Units),
                    Enrolled = grades.Count(g => g.AssignedCourse.TeacherId == group.Key)
                })
                .ToList();

            var submissionStatuses = teacherAssignments
                .Select(model => new TeacherSubmissionStatusModel
                {
                    CourseCode = model.CourseCode,
                    SubjectName = model.SubjectName,
                    Status = "--",
                    IsComplete = false
                })
                .ToList();

            return new TeacherDetailModel
            {
                TeacherId = 0,
                Name = "All Teachers",
                Department = "All Programs",
                Email = "--",
                Rank = "",
                TeachingLoadUnits = assignments.Sum(ac => ac.Units),
                TeachingLoadCount = assignments.Count,
                SectionCount = assignments.Count,
                PassRatePercent = 0m,
                SubmissionCompletionPercent = 0m,
                Assignments = teacherAssignments,
                CoursePassRates = new List<CoursePassRateModel>(),
                SubmissionStatuses = submissionStatuses,
                SubmissionSummary = submissionStatuses
                    .GroupBy(status => status.IsComplete)
                    .Select(group => new NamedValueModel
                    {
                        Name = group.Key ? "All grades submitted" : "Some grades are ungraded",
                        Value = group.Count()
                    })
                    .ToList(),
                PassCount = 0,
                FailCount = 0,
                IncompleteCount = submissionStatuses.Count,
                IsAggregate = true,
                ContextLabel = BuildContextLabel("All Teachers", schoolYear, termKey)
            };
        }

        private async Task<IList<ReportsProgramOptionModel>> LoadProgramOptionsAsync()
        {
            return await _ctx.Programs
                .Where(program => program.IsActive)
                .OrderBy(program => program.ProgramName)
                .AsNoTracking()
                .Select(program => new ReportsProgramOptionModel
                {
                    ProgramId = program.ProgramId,
                    ProgramCode = program.ProgramCode,
                    ProgramName = program.ProgramName
                })
                .ToListAsync();
        }

        private static string BuildScheduleDescription(IEnumerable<ClassSchedule> schedules)
        {
            var first = schedules.FirstOrDefault();
            if (first == null)
            {
                return "--";
            }

            return $"{first.Day.ToString()} {first.StartTime:hh\\:mm}-{first.EndTime:hh\\:mm}";
        }

        private static string DetermineSection(AssignedCourse course)
        {
            return course.Program?.ProgramName ?? "--";
        }

        private static string ResolveTeacherDepartment(IEnumerable<AssignedCourse> assignments, Teacher teacher)
        {
            var programName = assignments
                .Select(ac => ac.Program?.ProgramName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

            if (!string.IsNullOrWhiteSpace(programName))
            {
                return programName;
            }

            return teacher.Position ?? "--";
        }

        private static decimal? MapToPercent(IEnumerable<Grade> grades)
        {
            var finalGrades = grades.Select(MapToPercent).Where(value => value.HasValue).Select(value => value.Value).ToList();
            if (!finalGrades.Any())
            {
                return null;
            }

            return Math.Round(finalGrades.Average(), 2);
        }

        private static decimal? MapToPercent(Grade grade)
        {
            if (grade == null)
            {
                return null;
            }

            var components = new List<decimal?> { grade.Prelims, grade.Midterm, grade.SemiFinal, grade.Final };
            var available = components.Where(value => value.HasValue).Select(value => value.Value).ToList();
            if (!available.Any())
            {
                return null;
            }

            return Math.Round(available.Average(), 2);
        }

        private static string DetermineStudentStatus(Grade grade)
        {
            var forced = ForceFinalGradeValue(grade);
            return MapFinalGradeToStatus(forced);
        }

        private static string DetermineAggregateStatus(IEnumerable<Grade> grades)
        {
            var gradeList = grades.ToList();
            if (!gradeList.Any())
            {
                return "Incomplete";
            }

            if (gradeList.Any(g => !IsGradeComplete(g)))
            {
                return "Incomplete";
            }

            var finalGrades = gradeList
                .Select(ComputeFinalGradeValue)
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToList();

            if (!finalGrades.Any())
            {
                return "Incomplete";
            }

            var average = Math.Round(finalGrades.Average(), 2);
            return MapFinalGradeToStatus(average);
        }

        private class PassFailAccumulator
        {
            public int Pass { get; set; }
            public int Fail { get; set; }
            public int Incomplete { get; set; }
        }

        private static bool IsGradeComplete(Grade grade)
        {
            if (grade == null)
            {
                return false;
            }

            return grade.Prelims.HasValue
                && grade.Midterm.HasValue
                && grade.SemiFinal.HasValue
                && grade.Final.HasValue;
        }

        private static decimal? ComputeFinalGradeValue(Grade grade)
        {
            if (!IsGradeComplete(grade))
            {
                return null;
            }

            return MapToPercent(grade);
        }

        private static string MapFinalGradeToStatus(decimal? finalGrade)
        {
            if (!finalGrade.HasValue || finalGrade.Value < 1m)
            {
                return "Incomplete";
            }
            return finalGrade.Value < 3m ? "Passed" : "Failed";
        }

        private static decimal ComputePassRate(IEnumerable<decimal?> grades)
        {
            var validGrades = grades.Where(value => value.HasValue).Select(value => value.Value).ToList();
            if (!validGrades.Any())
            {
                return 0m;
            }

            var passed = validGrades.Count(value => value < 3m);
            return Math.Round((decimal)passed / validGrades.Count * 100m, 1);
        }

        private static string DetermineSubmissionStatus(IEnumerable<Grade> grades)
        {
            var gradeList = grades.ToList();
            if (gradeList.All(g => g.Final.HasValue))
            {
                return "Complete";
            }

            if (gradeList.Any(g => g.Final.HasValue))
            {
                return "Partial";
            }

            return "Missing";
        }

        private static decimal CalculateVolatility(IList<decimal> grades)
        {
            if (grades.Count <= 1)
            {
                return 0m;
            }

            var averages = grades.Select(g => (double)g).ToList();
            var mean = averages.Average();
            var variance = averages.Average(value => Math.Pow(value - mean, 2));
            return Math.Round((decimal)Math.Sqrt(variance), 2);
        }

        private static string MapVolatilityToLabel(decimal volatility)
        {
            if (volatility < 3m)
            {
                return "Stable";
            }

            if (volatility < 6m)
            {
                return "Steady Improver";
            }

            return "Volatile";
        }

        private static IList<StudentComparativeHighlightModel> BuildComparativeHighlights(IEnumerable<Grade> grades)
        {
            return grades
                .Where(grade => grade.AssignedCourse?.Course != null)
                .GroupBy(grade => grade.AssignedCourse.Course.CourseCode)
                .Select(group =>
                {
                    var best = group
                        .Select(grade => new
                        {
                            Grade = ComputeFinalGradeValue(grade),
                            Period = grade.AssignedCourse.Semester
                        })
                        .Where(x => x.Grade.HasValue && x.Grade.Value < 3m)
                        .OrderBy(x => x.Grade.Value)
                        .FirstOrDefault();

                    if (best == null)
                    {
                        return null;
                    }

                    return new StudentComparativeHighlightModel
                    {
                        Course = group.Key,
                        Grade = Math.Round(best.Grade.Value, 2),
                        PeriodLabel = ParseTerm(best.Period)?.Label ?? best.Period ?? "--"
                    };
                })
                .Where(item => item != null)
                .OrderBy(item => item.Grade)
                .ToList();
        }

        private static decimal ForceFinalGradeValue(Grade grade)
        {
            if (grade == null)
            {
                return 5m;
            }

            var computed = ComputeFinalGradeValue(grade);
            if (!computed.HasValue)
            {
                return 5m;
            }

            var rounded = Math.Round(computed.Value, 2);

            if (rounded < 1m)
            {
                rounded = 1m;
            }

            if (rounded > 5m)
            {
                rounded = 5m;
            }

            return rounded;
        }

        private static bool MatchesTermKey(string semester, string termKey)
        {
            if (IsAllSemester(termKey))
            {
                return true;
            }

            var normalizedKey = NormalizeStaticTermKey(termKey);
            var parsed = ParseTerm(semester);

            if (parsed == null)
            {
                return false;
            }

            var resolvedSemesterKey = NormalizeStaticTermKey(parsed.TermKey);
            return string.Equals(resolvedSemesterKey, normalizedKey, StringComparison.OrdinalIgnoreCase);
        }

        private static TermInfo ParseTerm(string semester)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return null;
            }

            var parts = semester.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            var termName = parts[0];
            var schoolYear = parts[1];
            return new TermInfo
            {
                TermKey = semester,
                Label = semester,
                SchoolYear = schoolYear,
                YearStart = ExtractYearStart(schoolYear),
                SemesterOrder = MapTermOrder(termName)
            };
        }

        private static int ExtractYearStart(string schoolYear)
        {
            if (string.IsNullOrWhiteSpace(schoolYear))
            {
                return 0;
            }

            var dashIndex = schoolYear.IndexOf('-');
            if (dashIndex <= 0)
            {
                return 0;
            }

            return int.TryParse(schoolYear.Substring(0, dashIndex), out var start) ? start : 0;
        }

        private static int MapTermOrder(string termName)
        {
            return termName switch
            {
                "Prelim" => 1,
                "Midterm" => 2,
                "SemiFinal" => 3,
                "Final" => 4,
                _ => 0
            };
        }
        private static readonly StaticSemesterDefinition[] StaticSemesterDefinitions = new[]
        {
            new StaticSemesterDefinition("all-semester", "All Semester", 0),
            new StaticSemesterDefinition("first-semester", "First Semester", 1),
            new StaticSemesterDefinition("second-semester", "Second Semester", 2)
        };
        private class TermInfo
        {
            public string TermKey { get; set; }
            public string Label { get; set; }
            public string SchoolYear { get; set; }
            public int YearStart { get; set; }
            public int SemesterOrder { get; set; }
        }

        private record StaticSemesterDefinition(string Key, string Label, int Order);
    }
}
