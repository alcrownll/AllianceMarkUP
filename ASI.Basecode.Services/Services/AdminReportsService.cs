using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class AdminReportsService : IAdminReportsService
    {
        private readonly IGradeRepository _grades;
        private readonly ITeacherRepository _teachers;
        private readonly IStudentRepository _students;
        private readonly IAssignedCourseRepository _assignedCourses;

        private const decimal AtRiskGwaThreshold = 2.5m;
        private const int UnitsTarget = 150;

        public AdminReportsService(
            IGradeRepository grades,
            ITeacherRepository teachers,
            IStudentRepository students,
            IAssignedCourseRepository assignedCourses)
        {
            _grades = grades;
            _teachers = teachers;
            _students = students;
            _assignedCourses = assignedCourses;
        }

        public async Task<ReportsDashboardModel> GetDashboardAsync(string schoolYear = null, string termKey = null, int? highlightedTeacherId = null, int? highlightedStudentId = null)
        {
            var availableSchoolYears = await GetAvailableSchoolYearsAsync();
            var normalizedSchoolYear = NormalizeSchoolYear(availableSchoolYears, schoolYear);

            var termOptions = await BuildTermOptionsAsync(normalizedSchoolYear);
            var normalizedTermKey = NormalizeTermKey(termOptions, termKey);

            var gradesScoped = await LoadGradesAsync(normalizedSchoolYear, normalizedTermKey);

            var summary = await BuildOverallSummaryAsync(normalizedSchoolYear, normalizedTermKey, gradesScoped, termOptions);
            var trend = await BuildEnrollmentTrendAsync(normalizedSchoolYear);
            var leaderboard = BuildProgramLeaderboard(gradesScoped);
            var demographics = await BuildDemographicsAsync(normalizedSchoolYear, normalizedTermKey);
            var outcomes = BuildCourseOutcomes(gradesScoped);
            var risks = BuildRiskIndicators(gradesScoped);
            var capacity = await BuildCapacityAsync(normalizedSchoolYear, normalizedTermKey);

            var teacherDirectory = await BuildTeacherDirectoryAsync(normalizedSchoolYear, normalizedTermKey);
            var teacherId = highlightedTeacherId ?? teacherDirectory.FirstOrDefault()?.TeacherId;
            var teacherDetail = teacherId.HasValue
                ? await GetTeacherDetailAsync(teacherId.Value, normalizedSchoolYear, normalizedTermKey)
                : new TeacherDetailModel();

            var studentOptions = await BuildStudentOptionsAsync();
            var studentId = highlightedStudentId ?? studentOptions.FirstOrDefault()?.StudentId;
            var studentAnalytics = studentId.HasValue
                ? await GetStudentAnalyticsAsync(studentId.Value, normalizedSchoolYear, normalizedTermKey)
                : new StudentAnalyticsModel();

            return new ReportsDashboardModel
            {
                SchoolYear = normalizedSchoolYear,
                TermKey = normalizedTermKey,
                AvailableSchoolYears = availableSchoolYears,
                TermOptions = termOptions,
                Overall = new ReportsOverallModel
                {
                    Summary = summary,
                    EnrollmentTrend = trend,
                    ProgramLeaderboard = leaderboard,
                    Demographics = demographics,
                    CourseOutcomes = outcomes,
                    RiskIndicators = risks,
                    Capacity = capacity
                },
                Teacher = new ReportsTeacherModel
                {
                    Directory = teacherDirectory,
                    SelectedTeacher = teacherDetail
                },
                Student = new ReportsStudentModel
                {
                    Students = studentOptions,
                    SelectedStudentId = studentId,
                    Analytics = studentAnalytics
                }
            };
        }

        public async Task<TeacherDetailModel> GetTeacherDetailAsync(int teacherId, string schoolYear = null, string termKey = null)
        {
            var teacher = await _teachers.GetTeachers()
                .Include(t => t.User)
                .ThenInclude(u => u.UserProfile)
                .Include(t => t.AssignedCourses)
                .ThenInclude(ac => ac.Course)
                .Include(t => t.AssignedCourses)
                .ThenInclude(ac => ac.Grades)
                .Include(t => t.AssignedCourses)
                .ThenInclude(ac => ac.ClassSchedules)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                return new TeacherDetailModel();
            }

            var scopedCourses = teacher.AssignedCourses
                .Where(ac => MatchesTerm(ac.Semester, schoolYear, termKey))
                .ToList();

            var grades = scopedCourses
                .SelectMany(ac => ac.Grades ?? Enumerable.Empty<Grade>())
                .ToList();

            var passCount = grades.Count(g => ExtractFinalScore(g) >= 75m);
            var gradedCount = grades.Count(g => ExtractFinalScore(g).HasValue);
            var passRate = gradedCount == 0 ? 0m : Math.Round(passCount * 100m / gradedCount, 1);

            var completionPercent = scopedCourses.Count == 0
                ? 0m
                : Math.Round(scopedCourses.Count(ac => ac.Grades.Any(g => ExtractFinalScore(g).HasValue)) * 100m / scopedCourses.Count, 1);

            return new TeacherDetailModel
            {
                TeacherId = teacher.TeacherId,
                Name = BuildName(teacher.User),
                Department = ResolveTeacherDepartment(teacher, scopedCourses),
                Email = teacher.User?.Email ?? string.Empty,
                Rank = teacher.Position,
                TeachingLoadUnits = scopedCourses.Sum(ac => ac.Units),
                SectionCount = scopedCourses.Count,
                PassRatePercent = passRate,
                SubmissionCompletionPercent = completionPercent,
                Assignments = scopedCourses.Select(ac => new TeacherAssignmentModel
                {
                    CourseCode = ac.Course?.CourseCode ?? ac.EDPCode,
                    Section = ac.EDPCode,
                    Schedule = BuildSchedule(ac.ClassSchedules),
                    Units = ac.Units,
                    Enrolled = ac.Grades?.Select(g => g.StudentId).Distinct().Count() ?? 0
                }).ToList(),
                CoursePassRates = scopedCourses.Select(ac =>
                {
                    var courseGrades = ac.Grades ?? new List<Grade>();
                    var graded = courseGrades.Where(g => ExtractFinalScore(g).HasValue).ToList();
                    var pass = graded.Count(g => ExtractFinalScore(g) >= 75m);
                    var rate = graded.Count == 0 ? 0m : Math.Round(pass * 100m / graded.Count, 1);
                    return new CoursePassRateModel
                    {
                        CourseCode = ac.Course?.CourseCode ?? ac.EDPCode,
                        PassRatePercent = rate
                    };
                }).ToList(),
                SubmissionStatuses = scopedCourses.Select(ac =>
                {
                    var hasFinals = ac.Grades.Any(g => g.Final.HasValue);
                    var status = hasFinals ? "Final grades submitted" : "Pending final grades";
                    return new TeacherSubmissionStatusModel
                    {
                        CourseCode = ac.Course?.CourseCode ?? ac.EDPCode,
                        Status = status,
                        IsComplete = hasFinals
                    };
                }).ToList()
            };
        }

        public async Task<StudentAnalyticsModel> GetStudentAnalyticsAsync(int studentId, string schoolYear = null, string termKey = null)
        {
            var gradeEntries = await _grades.GetGrades()
                .Include(g => g.AssignedCourse)
                .ThenInclude(ac => ac.Course)
                .Include(g => g.Student)
                .ThenInclude(s => s.User)
                .AsNoTracking()
                .Where(g => g.StudentId == studentId && g.AssignedCourse != null)
                .ToListAsync();

            gradeEntries = gradeEntries
                .Where(g => MatchesTerm(g.AssignedCourse.Semester, schoolYear, termKey))
                .ToList();

            if (!gradeEntries.Any())
            {
                return new StudentAnalyticsModel();
            }

            var gwaTrend = gradeEntries
                .GroupBy(g => g.AssignedCourse.Semester ?? "Unknown")
                .Select(g => new StudentTrendPointModel
                {
                    TermKey = g.Key,
                    Label = g.Key,
                    Gwa = MapPercentToGwa(g.Select(ExtractFinalScore).AverageOrDefault())
                })
                .OrderBy(g => g.TermKey)
                .ToList();

            var courseGrades = gradeEntries
                .Select(g => new StudentCourseGradeModel
                {
                    CourseCode = g.AssignedCourse?.Course?.CourseCode ?? g.AssignedCourse?.EDPCode,
                    Grade = ExtractFinalScore(g) ?? 0m
                })
                .ToList();

            var earnedUnits = gradeEntries
                .Where(g => ExtractFinalScore(g) >= 75m)
                .Sum(g => g.AssignedCourse?.Units ?? 0);

            var passCount = gradeEntries.Count(g => ExtractFinalScore(g) >= 75m);
            var failCount = gradeEntries.Count(g => ExtractFinalScore(g) < 75m);
            var incompleteCount = gradeEntries.Count(g => !ExtractFinalScore(g).HasValue);

            var topStrengths = courseGrades
                .OrderBy(g => g.Grade)
                .Take(3)
                .Select(g => $"A in {g.CourseCode}")
                .ToList();

            var riskCourses = courseGrades
                .Where(g => g.Grade < 75m)
                .OrderBy(g => g.Grade)
                .Take(3)
                .Select(g => $"Low grade in {g.CourseCode}")
                .ToList();

            return new StudentAnalyticsModel
            {
                GwaTrend = gwaTrend,
                CourseGrades = courseGrades,
                UnitsProgress = new StudentUnitsProgressModel
                {
                    EarnedUnits = earnedUnits,
                    RequiredUnits = UnitsTarget
                },
                StatusMix = new List<NamedValueModel>
                {
                    new NamedValueModel { Name = "Pass", Value = passCount },
                    new NamedValueModel { Name = "Fail", Value = failCount },
                    new NamedValueModel { Name = "Incomplete", Value = incompleteCount }
                },
                Strengths = topStrengths,
                Risks = riskCourses,
                Engagement = new StudentEngagementModel
                {
                    AttendancePercent = 0,
                    OnTimeSubmissionPercent = 0,
                    MissingWorkCount = incompleteCount
                }
            };
        }

        private async Task<IList<string>> GetAvailableSchoolYearsAsync()
        {
            var semesters = await _assignedCourses.GetAssignedCourses()
                .Where(ac => ac.Semester != null)
                .Select(ac => ac.Semester)
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            return semesters
                .Select(GetSchoolYearFromSemester)
                .Where(sy => !string.IsNullOrWhiteSpace(sy))
                .Distinct()
                .OrderBy(sy => sy)
                .ToList();
        }

        private async Task<IList<ReportTermOptionModel>> BuildTermOptionsAsync(string schoolYear)
        {
            if (string.IsNullOrWhiteSpace(schoolYear))
            {
                return new List<ReportTermOptionModel>();
            }

            var semesters = await _assignedCourses.GetAssignedCourses()
                .Where(ac => ac.Semester != null && ac.Semester.StartsWith(schoolYear))
                .Select(ac => ac.Semester)
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            var options = semesters
                .OrderBy(sem => sem)
                .Select(sem => new ReportTermOptionModel
                {
                    TermKey = sem,
                    Label = sem,
                    SchoolYear = schoolYear
                })
                .ToList();

            options.Insert(0, new ReportTermOptionModel
            {
                TermKey = string.Empty,
                Label = $"{schoolYear} - Whole Year",
                SchoolYear = schoolYear
            });

            return options;
        }

        private async Task<List<Grade>> LoadGradesAsync(string schoolYear, string termKey)
        {
            var query = _grades.GetGrades()
                .Include(g => g.Student)
                .ThenInclude(s => s.User)
                .ThenInclude(u => u.UserProfile)
                .Include(g => g.AssignedCourse)
                .ThenInclude(ac => ac.Course)
                .Include(g => g.AssignedCourse)
                .ThenInclude(ac => ac.Teacher)
                .ThenInclude(t => t.User)
                .AsNoTracking()
                .Where(g => g.AssignedCourse != null);

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                query = query.Where(g => g.AssignedCourse.Semester == termKey);
            }
            else if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                query = query.Where(g => g.AssignedCourse.Semester != null && g.AssignedCourse.Semester.StartsWith(schoolYear));
            }

            return await query.ToListAsync();
        }

        private async Task<ReportsOverallSummary> BuildOverallSummaryAsync(string schoolYear, string termKey, List<Grade> gradesScoped, IList<ReportTermOptionModel> termOptions)
        {
            var currentEnrollment = gradesScoped
                .Select(g => g.StudentId)
                .Distinct()
                .Count();

            var previousTermKey = termOptions
                .Select(o => o.TermKey)
                .Where(key => !string.IsNullOrEmpty(key))
                .Distinct()
                .Where(key => string.IsNullOrEmpty(termKey) || string.Compare(key, termKey, StringComparison.OrdinalIgnoreCase) < 0)
                .OrderByDescending(key => key)
                .FirstOrDefault();

            var previousEnrollment = await CountDistinctStudentsAsync(schoolYear, previousTermKey);

            var averageGwa = gradesScoped
                .Select(ExtractFinalScore)
                .Where(score => score.HasValue)
                .Select(score => MapPercentToGwa(score.Value))
                .DefaultIfEmpty(0m)
                .Average();

            var passCount = gradesScoped.Count(g => ExtractFinalScore(g) >= 75m);
            var graded = gradesScoped.Count(g => ExtractFinalScore(g).HasValue);
            var passRate = graded == 0 ? 0m : Math.Round(passCount * 100m / graded, 1);

            var returningStudents = gradesScoped
                .Where(g => g.Student != null && string.Equals(g.Student.AdmissionType, "old", StringComparison.OrdinalIgnoreCase))
                .Select(g => g.StudentId)
                .Distinct()
                .Count();

            var retention = currentEnrollment == 0 ? 0m : Math.Round(returningStudents * 100m / currentEnrollment, 1);

            return new ReportsOverallSummary
            {
                TotalEnrolled = currentEnrollment,
                GrowthPercent = ComputeChangePercent(previousEnrollment, currentEnrollment),
                AverageGwa = Math.Round(averageGwa, 2),
                PassRatePercent = passRate,
                RetentionPercent = retention
            };
        }

        private async Task<int> CountDistinctStudentsAsync(string schoolYear, string termKey)
        {
            var query = _grades.GetGrades()
                .Where(g => g.AssignedCourse != null)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                query = query.Where(g => g.AssignedCourse.Semester == termKey);
            }
            else if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                query = query.Where(g => g.AssignedCourse.Semester != null && g.AssignedCourse.Semester.StartsWith(schoolYear));
            }

            return await query
                .Select(g => g.StudentId)
                .Distinct()
                .CountAsync();
        }

        private async Task<IList<TrendPointModel>> BuildEnrollmentTrendAsync(string schoolYear)
        {
            var data = await _grades.GetGrades()
                .Where(g => g.AssignedCourse != null && g.AssignedCourse.Semester != null)
                .Select(g => new { g.AssignedCourse.Semester, g.StudentId })
                .AsNoTracking()
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                data = data.Where(x => x.Semester.StartsWith(schoolYear)).ToList();
            }

            return data
                .GroupBy(x => x.Semester)
                .OrderBy(g => g.Key)
                .Select(g => new TrendPointModel
                {
                    TermKey = g.Key,
                    Label = g.Key,
                    Value = g.Select(x => x.StudentId).Distinct().Count()
                })
                .ToList();
        }

        private IList<ProgramLeaderboardItemModel> BuildProgramLeaderboard(List<Grade> gradesScoped)
        {
            return gradesScoped
                .Where(g => g.Student != null)
                .GroupBy(g => g.Student.Program ?? "Unknown")
                .Select(g => new ProgramLeaderboardItemModel
                {
                    Program = g.Key,
                    Enrollment = g.Select(x => x.StudentId).Distinct().Count(),
                    GrowthPercent = 0m
                })
                .OrderByDescending(g => g.Enrollment)
                .ToList();
        }

        private async Task<DemographicBreakdownModel> BuildDemographicsAsync(string schoolYear, string termKey)
        {
            var gradesQuery = _grades.GetGrades()
                .Where(g => g.AssignedCourse != null);

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                gradesQuery = gradesQuery.Where(g => g.AssignedCourse.Semester == termKey);
            }
            else if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                gradesQuery = gradesQuery.Where(g => g.AssignedCourse.Semester != null && g.AssignedCourse.Semester.StartsWith(schoolYear));
            }

            var studentIdsQuery = gradesQuery
                .Select(g => g.StudentId)
                .Distinct();

            var students = await _students.GetStudents()
                .Where(s => studentIdsQuery.Contains(s.StudentId))
                .Include(s => s.User)
                .ThenInclude(u => u.UserProfile)
                .Include(s => s.Grades)
                .ThenInclude(g => g.AssignedCourse)
                .AsNoTracking()
                .ToListAsync();

            var genders = students
                .Select(s => string.IsNullOrWhiteSpace(s.User?.UserProfile?.Gender) ? "Unknown" : s.User.UserProfile.Gender)
                .GroupBy(g => g)
                .Select(g => new NamedValueModel { Name = g.Key, Value = g.Count() })
                .ToList();

            var ageBands = students
                .Select(s => s.User?.UserProfile?.DateOfBirth)
                .Where(dob => dob.HasValue)
                .Select(dob => CategorizeAge(dob.Value.ToAge()))
                .GroupBy(label => label)
                .Select(g => new NamedValueModel { Name = g.Key, Value = g.Count() })
                .OrderBy(g => g.Name)
                .ToList();

            var statuses = students
                .Select(s => string.IsNullOrWhiteSpace(s.StudentStatus) ? "Unknown" : s.StudentStatus)
                .GroupBy(status => status)
                .Select(g => new NamedValueModel { Name = g.Key, Value = g.Count() })
                .ToList();

            return new DemographicBreakdownModel
            {
                GenderSplit = genders,
                AgeBands = ageBands,
                Statuses = statuses
            };
        }

        private CourseOutcomeModel BuildCourseOutcomes(List<Grade> gradesScoped)
        {
            var courseGroups = gradesScoped
                .Where(g => g.AssignedCourse != null)
                .GroupBy(g => g.AssignedCourse.Course?.CourseCode ?? g.AssignedCourse.EDPCode)
                .Select(g => new
                {
                    Course = g.Key,
                    Grades = g.ToList()
                })
                .ToList();

            var failureRates = courseGroups
                .Select(g => new CourseStatModel
                {
                    CourseCode = g.Course,
                    MetricValue = ComputeFailureRate(g.Grades)
                })
                .OrderByDescending(c => c.MetricValue)
                .Take(5)
                .ToList();

            var bestPerforming = courseGroups
                .Select(g => new CourseStatModel
                {
                    CourseCode = g.Course,
                    MetricValue = ComputePassRate(g.Grades)
                })
                .OrderByDescending(c => c.MetricValue)
                .Take(5)
                .ToList();

            return new CourseOutcomeModel
            {
                HighestFailureRates = failureRates,
                BestPerforming = bestPerforming
            };
        }

        private List<RiskIndicatorModel> BuildRiskIndicators(List<Grade> gradesScoped)
        {
            var studentGwa = gradesScoped
                .GroupBy(g => g.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    Gwa = MapPercentToGwa(g.Select(ExtractFinalScore).AverageOrDefault())
                })
                .ToList();

            var riskStudents = studentGwa.Count(s => s.Gwa > AtRiskGwaThreshold && s.Gwa > 0);

            var sections = gradesScoped
                .GroupBy(g => g.AssignedCourseId)
                .Select(g => new
                {
                    Section = g.Key,
                    PassRate = ComputePassRate(g.ToList())
                })
                .ToList();

            var lowPassSections = sections.Count(s => s.PassRate < 70m);

            var pendingGrades = gradesScoped
                .Where(g => !g.Final.HasValue)
                .GroupBy(g => g.AssignedCourseId)
                .Count();

            return new List<RiskIndicatorModel>
            {
                new RiskIndicatorModel { Label = "Students below GWA threshold", Count = riskStudents },
                new RiskIndicatorModel { Label = "Sections pass rate < 70%", Count = lowPassSections },
                new RiskIndicatorModel { Label = "Courses with pending grades", Count = pendingGrades }
            };
        }

        private async Task<CapacityLoadModel> BuildCapacityAsync(string schoolYear, string termKey)
        {
            var sectionsQuery = _assignedCourses.GetAssignedCourses()
                .Include(ac => ac.Grades)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                sectionsQuery = sectionsQuery.Where(ac => ac.Semester == termKey);
            }
            else if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                sectionsQuery = sectionsQuery.Where(ac => ac.Semester != null && ac.Semester.StartsWith(schoolYear));
            }

            var sections = await sectionsQuery
                .AsNoTracking()
                .ToListAsync();

            var nearCapacity = sections.Count(ac => (ac.Grades?.Select(g => g.StudentId).Distinct().Count() ?? 0) >= 35);
            var highLoadFaculty = sections
                .GroupBy(ac => ac.TeacherId)
                .Count(g => g.Sum(ac => ac.Units) >= 24);
            var averageClassSize = sections.Count == 0
                ? 0m
                : Math.Round((decimal)sections.Average(ac => ac.Grades?.Select(g => g.StudentId).Distinct().Count() ?? 0), 1);

            return new CapacityLoadModel
            {
                SectionsNearCapacity = nearCapacity,
                FacultyHighLoad = highLoadFaculty,
                AverageClassSize = averageClassSize
            };
        }

        private async Task<IList<TeacherDirectoryItemModel>> BuildTeacherDirectoryAsync(string schoolYear, string termKey)
        {
            var teachers = await _teachers.GetTeachers()
                .Include(t => t.User)
                .Include(t => t.AssignedCourses)
                .ThenInclude(ac => ac.ClassSchedules)
                .AsNoTracking()
                .ToListAsync();

            return teachers.Select(t =>
            {
                var scopedCourses = t.AssignedCourses.Where(ac => MatchesTerm(ac.Semester, schoolYear, termKey)).ToList();
                return new TeacherDirectoryItemModel
                {
                    TeacherId = t.TeacherId,
                    Name = BuildName(t.User),
                    Department = ResolveTeacherDepartment(t, scopedCourses),
                    Email = t.User?.Email ?? string.Empty,
                    Rank = t.Position,
                    LoadUnits = scopedCourses.Sum(ac => ac.Units),
                    Sections = scopedCourses.Count
                };
            })
            .OrderByDescending(t => t.Sections)
            .ThenBy(t => t.Name)
            .ToList();
        }

        private async Task<IList<StudentOptionModel>> BuildStudentOptionsAsync()
        {
            var students = await _students.GetStudents()
                .Include(s => s.User)
                .AsNoTracking()
                .OrderBy(s => s.User.LastName)
                .ToListAsync();

            return students
                .Select(s => new StudentOptionModel
                {
                    StudentId = s.StudentId,
                    Name = BuildName(s.User),
                    Program = s.Program
                })
                .ToList();
        }

        private static string NormalizeSchoolYear(IList<string> availableSchoolYears, string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested) && availableSchoolYears.Contains(requested))
            {
                return requested;
            }

            return availableSchoolYears.LastOrDefault();
        }

        private static string NormalizeTermKey(IList<ReportTermOptionModel> terms, string requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                return string.Empty;
            }

            return terms.FirstOrDefault(t => string.Equals(t.TermKey, requested, StringComparison.OrdinalIgnoreCase))?.TermKey ?? string.Empty;
        }

        private static string GetSchoolYearFromSemester(string semester)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return null;
            }

            var parts = semester.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : semester;
        }

        private static bool MatchesTerm(string semester, string schoolYear, string termKey)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                return string.Equals(semester, termKey, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                return semester.StartsWith(schoolYear, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static decimal ComputeChangePercent(int previous, int current)
        {
            if (previous <= 0)
            {
                return current > 0 ? 100m : 0m;
            }

            return Math.Round(((decimal)current - previous) / previous * 100m, 1);
        }

        private static decimal? ExtractFinalScore(Grade grade)
        {
            if (grade == null)
            {
                return null;
            }

            var components = new (decimal? Score, decimal Weight)[]
            {
                (grade.Prelims, 0.3m),
                (grade.Midterm, 0.3m),
                (grade.SemiFinal, 0.2m),
                (grade.Final, 0.2m)
            };

            var weightedTotal = 0m;
            var weightSum = 0m;

            foreach (var component in components)
            {
                if (component.Score.HasValue)
                {
                    weightedTotal += component.Score.Value * component.Weight;
                    weightSum += component.Weight;
                }
            }

            if (weightSum <= 0)
            {
                return null;
            }

            return Math.Round(weightedTotal / weightSum, 2);
        }

        private static decimal ComputePassRate(IList<Grade> grades)
        {
            if (grades == null || grades.Count == 0)
            {
                return 0m;
            }

            var graded = grades.Where(g => ExtractFinalScore(g).HasValue).ToList();
            if (graded.Count == 0)
            {
                return 0m;
            }

            var pass = graded.Count(g => ExtractFinalScore(g) >= 75m);
            return Math.Round(pass * 100m / graded.Count, 1);
        }

        private static decimal ComputeFailureRate(IList<Grade> grades)
        {
            if (grades == null || grades.Count == 0)
            {
                return 0m;
            }

            var graded = grades.Where(g => ExtractFinalScore(g).HasValue).ToList();
            if (graded.Count == 0)
            {
                return 0m;
            }

            var failed = graded.Count(g => ExtractFinalScore(g) < 75m);
            return Math.Round(failed * 100m / graded.Count, 1);
        }

        private static decimal MapPercentToGwa(decimal percent)
        {
            if (percent >= 96) return 1.00m;
            if (percent >= 93) return 1.25m;
            if (percent >= 90) return 1.50m;
            if (percent >= 87) return 1.75m;
            if (percent >= 84) return 2.00m;
            if (percent >= 81) return 2.25m;
            if (percent >= 78) return 2.50m;
            if (percent >= 75) return 2.75m;
            if (percent >= 70) return 3.00m;
            if (percent >= 65) return 4.00m;
            return 5.00m;
        }

        private static string CategorizeAge(int age)
        {
            if (age < 18) return "<18";
            if (age <= 20) return "18-20";
            if (age <= 22) return "21-22";
            if (age <= 24) return "23-24";
            return "25+";
        }

        private static string BuildName(User user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            return string.Join(" ", new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static string BuildSchedule(IEnumerable<ClassSchedule> schedules)
        {
            if (schedules == null)
            {
                return string.Empty;
            }

            return string.Join(", ", schedules.Select(s =>
            {
                var start = s.StartTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
                var end = s.EndTime.ToString("hh\\:mm", CultureInfo.InvariantCulture);
                return $"{s.Day} {start}-{end}";
            }));
        }

        private static string ResolveTeacherDepartment(Teacher teacher, IList<AssignedCourse> scopedCourses)
        {
            var program = scopedCourses?
                .Select(ac => ac.Program)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            return program ?? "N/A";
        }
    }

    internal static class DateOnlyExtensions
    {
        public static int ToAge(this DateOnly date)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - date.Year;
            if (today < date.AddYears(age))
            {
                age--;
            }

            return age;
        }

        public static int? ToAge(this DateOnly? date)
        {
            return date.HasValue ? date.Value.ToAge() : (int?)null;
        }
    }

    internal static class EnumerableExtensions
    {
        public static decimal AverageOrDefault(this IEnumerable<decimal?> values)
        {
            var list = values.Where(v => v.HasValue).Select(v => v.Value).ToList();
            if (!list.Any())
            {
                return 0m;
            }

            return list.Average();
        }
    }
}
