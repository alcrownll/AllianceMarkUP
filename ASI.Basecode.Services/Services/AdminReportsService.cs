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

            var studentData = await BuildStudentOptionsAsync();
            var studentOptions = studentData.Options;
            var programs = studentData.Programs;
            var sections = studentData.Sections;
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
                    Analytics = studentAnalytics,
                    Programs = programs,
                    Sections = sections
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
                .ThenInclude(ac => ac.Program)
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
            var baseGrades = _grades.GetGrades()
                .Include(g => g.AssignedCourse)
                .ThenInclude(ac => ac.Course)
                .Include(g => g.Student)
                .ThenInclude(s => s.User)
                .ThenInclude(u => u.UserProfile)
                .AsNoTracking()
                .Where(g => g.AssignedCourse != null);

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                baseGrades = baseGrades.Where(g => g.AssignedCourse.Semester != null && g.AssignedCourse.Semester.StartsWith(termKey));
            }
            else if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                baseGrades = baseGrades.Where(g => g.AssignedCourse.Semester != null &&
                    (g.AssignedCourse.Semester.StartsWith(schoolYear) || !g.AssignedCourse.Semester.Contains("-")));
            }

            var gradeEntries = await baseGrades
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

            var ungradedRowsQuery = baseGrades
                .Where(g => g.Final == null)
                .GroupBy(g => new
                {
                    g.StudentId,
                    g.Student.User.IdNumber,
                    g.Student.User.LastName,
                    g.Student.User.FirstName,
                    g.Student.Program,
                    Gender = g.Student.User.UserProfile.Gender,
                    g.Student.YearLevel,
                    g.AssignedCourse.EDPCode
                });

            if (studentId > 0)
            {
                ungradedRowsQuery = ungradedRowsQuery.Where(group => group.Key.StudentId == studentId);
            }

            var ungradedRows = await ungradedRowsQuery
                .Select(group => new StudentSnapshotRowModel
                {
                    EdpCode = group.Key.EDPCode,
                    IdNumber = group.Key.IdNumber,
                    LastName = group.Key.LastName,
                    FirstName = group.Key.FirstName,
                    Program = group.Key.Program,
                    Gender = string.IsNullOrWhiteSpace(group.Key.Gender) ? "Unknown" : group.Key.Gender,
                    YearLevel = group.Key.YearLevel,
                    Gwa = null,
                    Status = "Ungraded"
                })
                .OrderBy(row => row.LastName)
                .ThenBy(row => row.FirstName)
                .ToListAsync();

            if (!gradeEntries.Any())
            {
                return new StudentAnalyticsModel
                {
                    Snapshot = ungradedRows
                };
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
            var semesters = await _grades.GetGrades()
                .Where(g => g.AssignedCourse != null && g.AssignedCourse.Semester != null)
                .Select(g => g.AssignedCourse.Semester)
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            var normalized = semesters
                .Select(GetSchoolYearFromSemester)
                .Where(sy => !string.IsNullOrWhiteSpace(sy))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalized.Any() || normalized.All(sy => sy.Length != 9 || !sy.Contains('-', StringComparison.Ordinal)))
            {
                normalized = BuildDefaultSchoolYears(3);
            }

            normalized.Sort((a, b) => string.CompareOrdinal(b, a));
            return normalized;
        }

        private async Task<IList<ReportTermOptionModel>> BuildTermOptionsAsync(string schoolYear)
        {
            if (string.IsNullOrWhiteSpace(schoolYear))
            {
                return new List<ReportTermOptionModel>();
            }

            var rawSemesters = await _grades.GetGrades()
                .Where(g => g.AssignedCourse != null && g.AssignedCourse.Semester != null)
                .Select(g => g.AssignedCourse.Semester)
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            var tokens = rawSemesters
                .Select(ExtractTermToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var fallbackToken in DefaultTermTokens)
            {
                if (!tokens.Contains(fallbackToken, StringComparer.OrdinalIgnoreCase))
                {
                    tokens.Add(fallbackToken);
                }
            }

            tokens.Sort((a, b) => string.CompareOrdinal(a, b));

            var options = tokens
                .Select(token => new ReportTermOptionModel
                {
                    TermKey = token,
                    Label = TermTokenToLabel(token),
                    SchoolYear = schoolYear
                })
                .ToList();

            options.Insert(0, new ReportTermOptionModel
            {
                TermKey = string.Empty,
                Label = "Whole Term",
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
                query = query.Where(g => g.AssignedCourse.Semester != null && g.AssignedCourse.Semester.StartsWith(termKey));
            }
            else if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                query = query.Where(g => g.AssignedCourse.Semester != null &&
                    (g.AssignedCourse.Semester.StartsWith(schoolYear) || !g.AssignedCourse.Semester.Contains("-")));
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
                data = data.Where(x => MatchesTerm(x.Semester, schoolYear, null)).ToList();
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

            var sections = students
                .Select(s => string.IsNullOrWhiteSpace(s.Section) ? "Unassigned" : s.Section)
                .GroupBy(sec => sec)
                .Select(g => new NamedValueModel { Name = g.Key, Value = g.Count() })
                .OrderBy(x => x.Name)
                .ToList();

            return new DemographicBreakdownModel
            {
                GenderSplit = genders,
                AgeBands = ageBands,
                Statuses = sections
            };
        }
        // VALIDATIONS
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

            return teachers.Select(t => {
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

        private async Task<(IList<StudentOptionModel> Options, IList<string> Programs, IList<string> Sections)> BuildStudentOptionsAsync()
        {
            var students = await _students.GetStudents()
                .Include(s => s.User)
                .Include(s => s.Grades)
                .ThenInclude(g => g.AssignedCourse)
                .AsNoTracking()
                .OrderBy(s => s.User.LastName)
                .ThenBy(s => s.User.FirstName)
                .ToListAsync();

            var options = students
                .Select(s => new StudentOptionModel
                {
                    StudentId = s.StudentId,
                    Name = BuildName(s.User),
                    Program = s.Program,
                    Sections = s.Grades?
                        .Select(g => g.AssignedCourse?.EDPCode)
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Distinct()
                        .OrderBy(code => code)
                        .ToList() ?? new List<string>()
                })
                .ToList();

            var programs = options
                .Select(o => o.Program)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p)
                .ToList();

            var sections = options
                .SelectMany(o => o.Sections)
                .Where(section => !string.IsNullOrWhiteSpace(section))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(section => section)
                .ToList();

            return (options, programs, sections);
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

            var normalized = semester.Trim();
            var hyphenParts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (hyphenParts.Length >= 2 && hyphenParts[0].Length == 4)
            {
                return $"{hyphenParts[0]}-{hyphenParts[1]}";
            }

            var spaceParts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (spaceParts.Length >= 2 && spaceParts[0].Length == 4)
            {
                return spaceParts[0];
            }

            var ordinal = ExtractTermToken(semester);
            if (!string.IsNullOrWhiteSpace(ordinal))
            {
                return InferSchoolYearForTermToken(ordinal);
            }

            return null;
        }

        private static string GetFriendlyTermLabel(string semester)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return semester;
            }

            var token = ExtractTermToken(semester);
            return TermTokenToLabel(token ?? semester);
        }

        private static string InferSchoolYearForTermToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var today = DateTime.UtcNow;
            var start = today.Month >= 6 ? today.Year : today.Year - 1;
            return $"{start}-{start + 1}";
        }

        private static string TermTokenToLabel(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            return token.EndsWith("st", StringComparison.OrdinalIgnoreCase) ? "1st Term"
                : token.EndsWith("nd", StringComparison.OrdinalIgnoreCase) ? "2nd Term"
                : token.EndsWith("rd", StringComparison.OrdinalIgnoreCase) ? "3rd Term"
                : token.EndsWith("th", StringComparison.OrdinalIgnoreCase) ? token
                : token;
        }

        private static string ExtractTermToken(string semester)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return null;
            }

            var normalized = semester.Trim();
            var hyphenParts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ordinalFromHyphen = hyphenParts.FirstOrDefault(IsOrdinalToken);
            if (!string.IsNullOrWhiteSpace(ordinalFromHyphen))
            {
                return ordinalFromHyphen;
            }

            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ordinal = parts.FirstOrDefault(IsOrdinalToken);
            return ordinal ?? (IsOrdinalToken(normalized) ? normalized : normalized);
        }

        private static List<string> BuildDefaultSchoolYears(int count)
        {
            var today = DateTime.UtcNow;
            var startYear = today.Month >= 6 ? today.Year : today.Year - 1;

            return Enumerable.Range(0, Math.Max(count, 1))
                .Select(offset => startYear - offset)
                .Select(year => $"{year}-{year + 1}")
                .ToList();
        }

        private static bool IsOrdinalToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return token.EndsWith("st", StringComparison.OrdinalIgnoreCase)
                || token.EndsWith("nd", StringComparison.OrdinalIgnoreCase)
                || token.EndsWith("rd", StringComparison.OrdinalIgnoreCase)
                || token.EndsWith("th", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> GetDefaultTermTokens()
        {
            return DefaultTermTokens;
        }

        private static readonly string[] DefaultTermTokens = { "1st", "2nd", "3rd" };

        private static bool MatchesTerm(string semester, string schoolYear, string termKey)
        {
            if (string.IsNullOrWhiteSpace(semester))
            {
                return string.IsNullOrWhiteSpace(termKey) && string.IsNullOrWhiteSpace(schoolYear);
            }

            if (!string.IsNullOrWhiteSpace(termKey))
            {
                return string.Equals(semester, termKey, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                if (semester.Contains('-', StringComparison.OrdinalIgnoreCase) || semester.Contains('/', StringComparison.OrdinalIgnoreCase))
                {
                    return semester.StartsWith(schoolYear, StringComparison.OrdinalIgnoreCase);
                }

                // legacy semesters like "1st" do not embed the school year; include them when filtering by year
                return true;
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
            if (percent <= 5m && percent > 0m)
            {
                return Math.Round(percent, 2);
            }

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
            var programCode = scopedCourses?
                .Select(ac => ac.Program?.ProgramCode)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

            return programCode ?? "N/A";
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
