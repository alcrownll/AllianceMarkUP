using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class AdminReportsService : IAdminReportsService
    {
        private readonly AsiBasecodeDBContext _ctx;

        public AdminReportsService(AsiBasecodeDBContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<ReportsDashboardModel> GetDashboardAsync(string schoolYear = null, string termKey = null, int? highlightedTeacherId = null, int? highlightedStudentId = null)
        {
            var normalizedSchoolYear = await ResolveSchoolYearAsync(schoolYear);
            var termOptions = await BuildTermOptionsAsync(normalizedSchoolYear);
            var normalizedTermKey = NormalizeTermKey(termKey, termOptions);

            var students = await LoadStudentDirectoryAsync(normalizedSchoolYear, normalizedTermKey);
            var teachers = await LoadTeacherDirectoryAsync(normalizedSchoolYear, normalizedTermKey);

            StudentAnalyticsModel studentAnalytics = null;
            if (highlightedStudentId.HasValue)
            {
                studentAnalytics = await GetStudentAnalyticsAsync(highlightedStudentId.Value, normalizedSchoolYear, normalizedTermKey);
            }

            TeacherDetailModel teacherDetail = null;
            if (highlightedTeacherId.HasValue)
            {
                teacherDetail = await GetTeacherDetailAsync(highlightedTeacherId.Value, normalizedSchoolYear, normalizedTermKey);
            }

            return new ReportsDashboardModel
            {
                SchoolYear = normalizedSchoolYear,
                TermKey = normalizedTermKey,
                AvailableSchoolYears = await GetAvailableSchoolYearsAsync(),
                TermOptions = termOptions,
                Student = new ReportsStudentModel
                {
                    Students = students,
                    SelectedStudentId = highlightedStudentId,
                    Analytics = studentAnalytics ?? new StudentAnalyticsModel()
                },
                Teacher = new ReportsTeacherModel
                {
                    Directory = teachers,
                    SelectedTeacher = teacherDetail ?? new TeacherDetailModel()
                }
            };
        }

        public async Task<TeacherDetailModel> GetTeacherDetailAsync(int teacherId, string schoolYear = null, string termKey = null)
        {
            var normalizedSchoolYear = await ResolveSchoolYearAsync(schoolYear);
            var termOptions = await BuildTermOptionsAsync(normalizedSchoolYear);
            var normalizedTermKey = NormalizeTermKey(termKey, termOptions);

            var teacher = await _ctx.Teachers
                .Include(t => t.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                return new TeacherDetailModel();
            }

            var assignedCourses = await _ctx.AssignedCourses
                .Where(ac => ac.TeacherId == teacherId && ac.SchoolYear == normalizedSchoolYear)
                .Include(ac => ac.Course)
                .Include(ac => ac.Program)
                .Include(ac => ac.ClassSchedules)
                .AsNoTracking()
                .ToListAsync();

            if (!string.IsNullOrEmpty(normalizedTermKey))
            {
                assignedCourses = assignedCourses
                    .Where(ac => MatchesTermKey(ac.Semester, normalizedTermKey))
                    .ToList();
            }

            var grades = await _ctx.Grades
                .Where(g => g.AssignedCourse.TeacherId == teacherId && g.AssignedCourse.SchoolYear == normalizedSchoolYear)
                .Include(g => g.AssignedCourse)
                .AsNoTracking()
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(normalizedTermKey))
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

                var outcomes = courseGrades
                    .Select(ComputeFinalGradeValue)
                    .Where(value => value.HasValue && value.Value >= 1m)
                    .Select(value => MapFinalGradeToStatus(value.Value))
                    .ToList();

                var passCount = outcomes.Count(result => result == "Passed");
                var failCount = outcomes.Count(result => result == "Failed");
                var total = passCount + failCount;
                var passRate = total > 0 ? Math.Round(passCount * 100m / total, 1) : 0m;

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

            var gradeOutcomes = grades
                .Select(ComputeFinalGradeValue)
                .Where(value => value.HasValue && value.Value >= 1m)
                .Select(value => MapFinalGradeToStatus(value.Value))
                .ToList();

            var aggregatePassCount = gradeOutcomes.Count(result => result == "Passed");
            var aggregateFailCount = gradeOutcomes.Count(result => result == "Failed");
            var aggregateTotal = aggregatePassCount + aggregateFailCount;
            var passRateAggregate = aggregateTotal > 0 ? Math.Round(aggregatePassCount * 100m / aggregateTotal, 1) : 0m;

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
                IncompleteCount = passFailCounts.Incomplete
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
                .GroupBy(g => g.AssignedCourse.Course.CourseCode)
                .Select(group => new StudentCourseGradeModel
                {
                    CourseCode = group.Key,
                    Grade = MapToPercent(group) ?? 0m
                })
                .OrderBy(g => g.CourseCode)
                .ToList();

            var gradeBreakdown = grades.Select(g => new StudentGradeBreakdownModel
            {
                EdpCode = g.AssignedCourse.EDPCode,
                Subject = g.AssignedCourse.Course.Description,
                Prelim = g.Prelims,
                Midterm = g.Midterm,
                Prefinal = g.SemiFinal,
                Final = g.Final,
                FinalGrade = MapToPercent(g),
                Status = DetermineStudentStatus(g)
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
                ComparativeHighlights = comparativeHighlights
            };
        }

        private async Task<string> ResolveSchoolYearAsync(string schoolYear)
        {
            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                return schoolYear;
            }

            var mostRecent = await _ctx.AssignedCourses
                .Where(ac => ac.SchoolYear != null)
                .OrderByDescending(ac => ac.SchoolYear)
                .Select(ac => ac.SchoolYear)
                .FirstOrDefaultAsync();

            return mostRecent ?? string.Empty;
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

        private async Task<IList<ReportTermOptionModel>> BuildTermOptionsAsync(string schoolYear)
        {
            if (string.IsNullOrWhiteSpace(schoolYear))
            {
                return new List<ReportTermOptionModel>();
            }

            var semesters = await _ctx.AssignedCourses
                .Where(ac => ac.SchoolYear == schoolYear && ac.Semester != null)
                .Select(ac => ac.Semester)
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            var parsed = semesters
                .Select(ParseTerm)
                .Where(term => term != null)
                .OrderBy(term => term.YearStart)
                .ThenBy(term => term.SemesterOrder)
                .Select(term => new ReportTermOptionModel
                {
                    TermKey = term.TermKey,
                    Label = term.Label,
                    SchoolYear = term.SchoolYear
                })
                .ToList();

            if (parsed.Count > 0)
            {
                parsed.Insert(0, new ReportTermOptionModel
                {
                    TermKey = string.Empty,
                    Label = $"{schoolYear} - Whole Year",
                    SchoolYear = schoolYear
                });
            }

            return parsed;
        }

        private static string NormalizeTermKey(string termKey, IList<ReportTermOptionModel> options)
        {
            if (string.IsNullOrWhiteSpace(termKey))
            {
                return string.Empty;
            }

            return options.FirstOrDefault(option => string.Equals(option.TermKey, termKey, StringComparison.OrdinalIgnoreCase))?.TermKey ?? string.Empty;
        }

        private async Task<IList<StudentOptionModel>> LoadStudentDirectoryAsync(string schoolYear, string termKey)
        {
            var students = await _ctx.Students
                .Include(s => s.User)
                .AsNoTracking()
                .OrderBy(s => s.User.LastName)
                .ThenBy(s => s.User.FirstName)
                .ToListAsync();

            return students
                .Select(student => new StudentOptionModel
                {
                    StudentId = student.StudentId,
                    Name = student.User != null ? $"{student.User.FirstName} {student.User.LastName}" : "Student",
                    Program = student.Program,
                    Sections = new List<string>()
                })
                .ToList();
        }

        private async Task<IList<TeacherDirectoryItemModel>> LoadTeacherDirectoryAsync(string schoolYear, string termKey)
        {
            var teachers = await _ctx.Teachers
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();

            var assignments = await _ctx.AssignedCourses
                .Where(ac => ac.SchoolYear == schoolYear)
                .Include(ac => ac.Program)
                .AsNoTracking()
                .ToListAsync();

            if (!string.IsNullOrEmpty(termKey))
            {
                assignments = assignments
                    .Where(ac => MatchesTermKey(ac.Semester, termKey))
                    .ToList();
            }

            var stats = assignments
                .GroupBy(ac => ac.TeacherId)
                .ToDictionary(
                    group => group.Key,
                    group => new
                    {
                        LoadUnits = group.Sum(ac => ac.Units),
                        Sections = group.Count(),
                        Department = group.Select(ac => ac.Program?.ProgramName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    });

            return teachers
                .Select(teacher =>
                {
                    stats.TryGetValue(teacher.TeacherId, out var stat);

                    return new TeacherDirectoryItemModel
                    {
                        TeacherId = teacher.TeacherId,
                        Name = teacher.User != null ? $"{teacher.User.FirstName} {teacher.User.LastName}" : "Teacher",
                        Department = stat?.Department ?? teacher.Position ?? "--",
                        Email = teacher.User?.Email ?? "--",
                        Rank = teacher.Position,
                        LoadUnits = stat?.LoadUnits ?? 0,
                        Sections = stat?.Sections ?? 0
                    };
                })
                .OrderBy(item => item.Name)
                .ToList();
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
            var finalGrade = ComputeFinalGradeValue(grade);
            return MapFinalGradeToStatus(finalGrade);
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
                            Grade = MapToPercent(grade),
                            Period = grade.AssignedCourse.Semester
                        })
                        .Where(x => x.Grade.HasValue)
                        .OrderByDescending(x => x.Grade)
                        .FirstOrDefault();

                    return new StudentComparativeHighlightModel
                    {
                        Course = group.Key,
                        Grade = best?.Grade,
                        PeriodLabel = best != null ? ParseTerm(best.Period)?.Label ?? best.Period : "--"
                    };
                })
                .Where(item => item.Grade.HasValue)
                .OrderByDescending(item => item.Grade)
                .ToList();
        }

        private static bool MatchesTermKey(string semester, string termKey)
        {
            if (string.IsNullOrWhiteSpace(termKey))
            {
                return true;
            }

            var term = ParseTerm(semester);
            return term != null && string.Equals(term.TermKey, termKey, StringComparison.OrdinalIgnoreCase);
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

        private class TermInfo
        {
            public string TermKey { get; set; }
            public string Label { get; set; }
            public string SchoolYear { get; set; }
            public int YearStart { get; set; }
            public int SemesterOrder { get; set; }
        }
    }
}
