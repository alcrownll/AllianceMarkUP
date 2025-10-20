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
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly AsiBasecodeDBContext _ctx;

        public AdminDashboardService(AsiBasecodeDBContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<DashboardSummaryModel> GetSummaryAsync()
        {
            var currentTerm = GetCurrentTerm();
            var previousTerm = GetPreviousTerm(currentTerm);

            var totalStudents = await _ctx.Students.CountAsync();
            var totalTeachers = await _ctx.Teachers.CountAsync();
            var activeCourses = await _ctx.Courses.CountAsync();
            var previousCourses = activeCourses;

            var currentEnrollment = await CountEnrolledStudentsAsync(currentTerm.TermKey);
            var previousEnrollment = await CountEnrolledStudentsAsync(previousTerm.TermKey);

            return new DashboardSummaryModel
            {
                TotalStudents = totalStudents,
                StudentsChangePercent = ComputeChangePercent(previousEnrollment, currentEnrollment),
                TotalTeachers = totalTeachers,
                TeachersChangePercent = 0,
                ActiveCourses = activeCourses,
                ActiveCoursesChangePercent = ComputeChangePercent(previousCourses, activeCourses)
            };
        }

        public async Task<IList<EnrollmentTrendPointModel>> GetEnrollmentTrendAsync(int maxPoints = 8)
        {
            var projections = await _ctx.Grades
                .Where(g => g.AssignedCourse != null)
                .Select(g => new
                {
                    g.StudentId,
                    g.AssignedCourse.Semester,
                    g.AssignedCourse.SchoolYear
                })
                .AsNoTracking()
                .ToListAsync();

            var trend = projections
                .Select(x => ParseTerm(x.Semester, x.SchoolYear))
                .Where(t => t != null)
                .GroupBy(t => t.TermKey)
                .Select(g => new EnrollmentTrendPointModel
                {
                    TermKey = g.Key,
                    Label = g.First().Label,
                    YearStart = g.First().YearStart,
                    SemesterOrder = g.First().SemesterOrder,
                    StudentCount = projections
                        .Where(p => MatchesTermKey(p.Semester, p.SchoolYear, g.Key))
                        .GroupBy(p => p.StudentId)
                        .Count()
                })
                .OrderBy(p => p.YearStart)
                .ThenBy(p => p.SemesterOrder)
                .ToList();

            if (maxPoints > 0 && trend.Count > maxPoints)
            {
                trend = trend.Skip(Math.Max(0, trend.Count - maxPoints)).ToList();
            }

            return trend;
        }

        public async Task<DashboardYearDetailModel> GetYearDetailAsync(string schoolYear = null, string termKey = null)
        {
            var termInfo = await BuildTermInfoForYearAsync(schoolYear);
            var targetYear = termInfo.TargetSchoolYear;

            var grades = await _ctx.Grades
                .Where(g => g.AssignedCourse != null)
                .Include(g => g.Student)
                .Include(g => g.AssignedCourse)
                    .ThenInclude(ac => ac.Course)
                .AsNoTracking()
                .ToListAsync();

            var allEntries = grades
                .Select(g => new
                {
                    Grade = g,
                    Term = ParseTerm(g.AssignedCourse.Semester, g.AssignedCourse.SchoolYear)
                })
                .Where(x => x.Term != null)
                .ToList();

            if (string.IsNullOrWhiteSpace(targetYear))
            {
                targetYear = allEntries
                    .Select(x => x.Term.SchoolYear)
                    .Where(sy => !string.IsNullOrWhiteSpace(sy))
                    .OrderBy(sy => sy)
                    .LastOrDefault();
            }

            var yearEntries = allEntries
                .Where(x => string.IsNullOrWhiteSpace(targetYear)
                            || string.Equals(x.Term.SchoolYear, targetYear, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!yearEntries.Any() && allEntries.Any())
            {
                yearEntries = allEntries;
                targetYear = yearEntries
                    .Select(x => x.Term.SchoolYear)
                    .Where(sy => !string.IsNullOrWhiteSpace(sy))
                    .OrderBy(sy => sy)
                    .LastOrDefault();
            }

            var termOptions = yearEntries
                .GroupBy(x => x.Term.TermKey)
                .Select(g => new TermOptionModel
                {
                    TermKey = g.Key,
                    Label = g.First().Term.Label,
                    SchoolYear = g.First().Term.SchoolYear,
                    YearStart = g.First().Term.YearStart,
                    SemesterOrder = g.First().Term.SemesterOrder
                })
                .OrderBy(t => t.YearStart)
                .ThenBy(t => t.SemesterOrder)
                .ToList();

            var estimatedYearStart = termOptions.FirstOrDefault()?.YearStart ?? EstimateYearStart(targetYear);
            var wholeYearLabel = !string.IsNullOrWhiteSpace(targetYear)
                ? $"{targetYear} - Whole Year"
                : "Whole Year";

            if (termOptions.Any())
            {
                if (!termOptions.Any(t => string.IsNullOrEmpty(t.TermKey)))
                {
                    termOptions.Insert(0, new TermOptionModel
                    {
                        TermKey = string.Empty,
                        Label = wholeYearLabel,
                        SchoolYear = targetYear,
                        YearStart = estimatedYearStart,
                        SemesterOrder = 0
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(targetYear))
            {
                termOptions.Add(new TermOptionModel
                {
                    TermKey = string.Empty,
                    Label = wholeYearLabel,
                    SchoolYear = targetYear,
                    YearStart = estimatedYearStart,
                    SemesterOrder = 0
                });
            }

            string normalizedTermKey = null;
            if (!string.IsNullOrWhiteSpace(termKey))
            {
                normalizedTermKey = termOptions
                    .FirstOrDefault(t => string.Equals(t.TermKey, termKey, StringComparison.OrdinalIgnoreCase))
                    ?.TermKey;
            }

            var scopedEntries = !string.IsNullOrEmpty(normalizedTermKey)
                ? yearEntries.Where(x => string.Equals(x.Term.TermKey, normalizedTermKey, StringComparison.OrdinalIgnoreCase)).ToList()
                : yearEntries;

            var studentsByYear = yearEntries
                .Where(x => x.Grade.Student != null)
                .GroupBy(x => x.Grade.StudentId)
                .Select(grp => new StudentSnapshot
                {
                    StudentId = grp.Key,
                    Program = grp.First().Grade.Student?.Program ?? "N/A",
                    YearLevel = NormalizeYearLevel(grp.First().Grade.Student?.YearLevel),
                    Grades = grp.Select(x => x.Grade).ToList()
                })
                .ToList();

            var totalUniqueStudents = studentsByYear.Count;

            var programShares = studentsByYear
                .GroupBy(s => s.Program)
                .Select(g => new ProgramShareModel
                {
                    Program = g.Key,
                    StudentCount = g.Count(),
                    SharePercent = totalUniqueStudents == 0 ? 0 : Math.Round((decimal)g.Count() / totalUniqueStudents * 100m, 1)
                })
                .OrderByDescending(p => p.StudentCount)
                .ToList();

            var yearLevelSeries = studentsByYear
                .GroupBy(s => new { s.Program, s.YearLevel })
                .Select(g => new YearLevelSeriesPointModel
                {
                    Program = g.Key.Program,
                    YearLevel = g.Key.YearLevel,
                    Count = g.Count()
                })
                .OrderBy(g => g.Program)
                .ThenBy(g => g.YearLevel)
                .ToList();

            var gpaTrend = termOptions.Count > 0
                ? termOptions.Select(option =>
                {
                    var termGrades = string.IsNullOrEmpty(option.TermKey)
                        ? yearEntries
                        : yearEntries.Where(x => string.Equals(x.Term.TermKey, option.TermKey, StringComparison.OrdinalIgnoreCase));

                    return new GpaTrendPointModel
                    {
                        TermKey = option.TermKey,
                        Label = option.Label,
                        AverageGpa = ComputeAverageGpa(termGrades.Select(x => ExtractGradeScore(x.Grade)))
                    };
                }).ToList()
                : new List<GpaTrendPointModel>();

            var scopedGrades = scopedEntries
                .Select(x => x.Grade)
                .Where(g => g.AssignedCourse != null)
                .ToList();

            var subjectEnrollments = scopedGrades
                .GroupBy(g => new
                {
                    Code = g.AssignedCourse.Course?.CourseCode
                        ?? g.AssignedCourse.EDPCode
                        ?? $"COURSE-{g.AssignedCourseId}",
                    Name = g.AssignedCourse.Course?.Description
                        ?? g.AssignedCourse.EDPCode
                        ?? g.AssignedCourse.Course?.CourseCode
                        ?? $"Course {g.AssignedCourseId}"
                })
                .Select(g => new SubjectEnrollmentPointModel
                {
                    CourseCode = g.Key.Code,
                    CourseName = g.Key.Name,
                    StudentCount = g.Select(x => x.StudentId).Distinct().Count()
                })
                .OrderByDescending(s => s.StudentCount)
                .ThenBy(s => s.CourseCode)
                .Take(15)
                .ToList();

            var completeGrades = scopedGrades
                .Where(IsGradeComplete)
                .ToList();

            var subjectAverageGpa = completeGrades
                .GroupBy(g => new
                {
                    Code = g.AssignedCourse.Course?.CourseCode
                        ?? g.AssignedCourse.EDPCode
                        ?? $"COURSE-{g.AssignedCourseId}",
                    Name = g.AssignedCourse.Course?.Description
                        ?? g.AssignedCourse.EDPCode
                        ?? g.AssignedCourse.Course?.CourseCode
                        ?? $"Course {g.AssignedCourseId}"
                })
                .Select(g => new SubjectGpaPointModel
                {
                    CourseCode = g.Key.Code,
                    CourseName = g.Key.Name,
                    AverageGpa = ComputeAverageGpa(g.Select(ExtractGradeScore))
                })
                .OrderByDescending(s => s.AverageGpa)
                .ToList();

            var finalSnapshots = completeGrades
                .Select(g => new
                {
                    g.StudentId,
                    Score = ExtractGradeScore(g)
                })
                .Where(x => x.Score.HasValue)
                .Select(x => new
                {
                    x.StudentId,
                    Score = x.Score.Value
                })
                .ToList();

            var totalPassed = finalSnapshots.Count(x => x.Score <= 3m);
            var totalFailed = finalSnapshots.Count(x => x.Score > 3m);
            var passFailTotal = totalPassed + totalFailed;

            var passFail = new List<PassFailRateModel>
            {
                new PassFailRateModel
                {
                    Program = "All Subjects",
                    Passed = totalPassed,
                    Failed = totalFailed,
                    PassRate = passFailTotal == 0 ? 0 : Math.Round((decimal)totalPassed / passFailTotal * 100m, 1),
                    FailRate = passFailTotal == 0 ? 0 : Math.Round((decimal)totalFailed / passFailTotal * 100m, 1),
                    TermKey = string.Empty,
                    TermLabel = "All Records"
                }
            };

            var totalEvaluatedCourses = totalPassed + totalFailed;
            var overallPassRate = totalEvaluatedCourses == 0
                ? 0m
                : Math.Round((decimal)totalPassed / totalEvaluatedCourses * 100m, 1);

            return new DashboardYearDetailModel
            {
                SchoolYear = targetYear,
                ProgramShares = programShares,
                YearLevelSeries = yearLevelSeries,
                AverageGpa = gpaTrend,
                PassFailRates = passFail,
                TermOptions = termOptions,
                SelectedTermKey = normalizedTermKey ?? string.Empty,
                OverallPassRate = overallPassRate,
                SubjectEnrollments = subjectEnrollments,
                SubjectAverageGpa = subjectAverageGpa
            };
        }

        private static int EstimateYearStart(string schoolYear)
        {
            if (string.IsNullOrWhiteSpace(schoolYear))
            {
                return 0;
            }

            var parts = schoolYear.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out var start))
            {
                return start;
            }

            return 0;
        }

        private static decimal? ExtractFinalScore(Grade grade)
        {
            if (grade?.Final == null)
            {
                return null;
            }

            var score = grade.Final.Value;

            if (score > 5m)
            {
                return MapPercentToGwa(score);
            }

            return score >= 1m ? score : null;
        }

        private static decimal? ExtractGradeScore(Grade grade)
        {
            var finalScore = ExtractFinalScore(grade);
            if (finalScore.HasValue)
            {
                return finalScore;
            }

            if (grade == null)
            {
                return null;
            }

            var componentScores = new List<decimal>();

            if (grade.Prelims.HasValue)
            {
                componentScores.Add(grade.Prelims.Value);
            }

            if (grade.Midterm.HasValue)
            {
                componentScores.Add(grade.Midterm.Value);
            }

            if (grade.SemiFinal.HasValue)
            {
                componentScores.Add(grade.SemiFinal.Value);
            }

            if (!componentScores.Any())
            {
                return null;
            }

            var average = componentScores.Average();

            if (average > 5m)
            {
                average = MapPercentToGwa(average);
            }

            if (average < 1m)
            {
                return null;
            }

            return Math.Min(Math.Max(Math.Round(average, 2), 1m), 5m);
        }

        private async Task<int> CountEnrolledStudentsAsync(string termKey)
        {
            if (string.IsNullOrEmpty(termKey))
            {
                return await _ctx.Grades
                    .Select(g => g.StudentId)
                    .Distinct()
                    .CountAsync();
            }

            var enrollments = await _ctx.Grades
                .Where(g => g.AssignedCourse != null)
                .Select(g => new { g.StudentId, g.AssignedCourse.Semester, g.AssignedCourse.SchoolYear })
                .ToListAsync();

            return enrollments
                .Where(e => MatchesTermKey(e.Semester, e.SchoolYear, termKey))
                .Select(e => e.StudentId)
                .Distinct()
                .Count();
        }

        private static decimal ComputeAverageGpa(IEnumerable<decimal?> grades)
        {
            var list = grades
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToList();

            if (!list.Any())
            {
                return 0m;
            }

            return Math.Round(list.Average(), 2);
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

        private static decimal ComputeChangePercent(int previous, int current)
        {
            if (previous <= 0)
            {
                return current > 0 ? 100m : 0m;
            }

            return Math.Round(((decimal)current - previous) / previous * 100m, 1);
        }

        private static bool IsGradeComplete(Grade grade)
        {
            if (grade == null)
            {
                return false;
            }

            return grade.Final.HasValue
                || grade.Prelims.HasValue
                || grade.Midterm.HasValue
                || grade.SemiFinal.HasValue;
        }

        public async Task<IList<string>> GetAvailableSchoolYearsAsync()
        {
            var assignedCourses = await _ctx.AssignedCourses
                .Where(ac => ac.Semester != null || ac.SchoolYear != null)
                .AsNoTracking()
                .ToListAsync();

            return assignedCourses
                .Select(ac => ParseTerm(ac.Semester, ac.SchoolYear) ?? BuildWholeYearTermFromSchoolYear(ac.SchoolYear))
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.SchoolYear))
                .Select(t => t.SchoolYear)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(sy => sy)
                .ToList();
        }

        private static TermInfo BuildWholeYearTermFromSchoolYear(string schoolYear)
        {
            if (!TryExtractSchoolYearRange(schoolYear, out var startYear, out var endYear))
            {
                return null;
            }

            return new TermInfo
            {
                SchoolYear = $"{startYear}-{endYear}",
                YearStart = startYear,
                SemesterOrder = 0,
                TermKey = $"{startYear}-{endYear}-0",
                Label = $"{startYear}-{endYear} - Whole Year"
            };
        }

        private static TermInfo ParseTerm(string rawSemester, string fallbackSchoolYear = null)
        {
            if (string.IsNullOrWhiteSpace(rawSemester) && string.IsNullOrWhiteSpace(fallbackSchoolYear))
            {
                return null;
            }

            var trimmed = rawSemester?.Trim() ?? string.Empty;

            var directMatch = Regex.Match(trimmed, @"(20\d{2})\s*[-/]\s*(20\d{2})\s*-\s*(\d)");
            if (directMatch.Success)
            {
                var start = int.Parse(directMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var end = int.Parse(directMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                var sem = int.Parse(directMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                return BuildTermInfo(start, end, sem);
            }

            if (TryExtractSchoolYearRange(trimmed, out var semesterStart, out var semesterEnd))
            {
                var semOrder = ExtractSemesterOrder(trimmed);
                return BuildTermInfo(semesterStart, semesterEnd, semOrder);
            }

            if (TryExtractSchoolYearRange(fallbackSchoolYear, out var startYear, out var endYear))
            {
                var semOrder = ExtractSemesterOrder(trimmed);
                return BuildTermInfo(startYear, endYear, semOrder);
            }

            return null;
        }

        private static bool TryExtractSchoolYearRange(string source, out int startYear, out int endYear)
        {
            startYear = 0;
            endYear = 0;

            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var matches = Regex.Matches(source, @"20\d{2}");
            if (matches.Count >= 2)
            {
                startYear = int.Parse(matches[0].Value, CultureInfo.InvariantCulture);
                endYear = int.Parse(matches[1].Value, CultureInfo.InvariantCulture);
            }
            else if (matches.Count == 1)
            {
                startYear = int.Parse(matches[0].Value, CultureInfo.InvariantCulture);
                endYear = startYear + 1;
            }
            else
            {
                return false;
            }

            if (endYear <= startYear)
            {
                endYear = startYear + 1;
            }

            return true;
        }

        private async Task<TermInfoResult> BuildTermInfoForYearAsync(string schoolYear)
        {
            var availableYears = await GetAvailableSchoolYearsAsync();

            string targetYear = null;
            if (!string.IsNullOrWhiteSpace(schoolYear))
            {
                targetYear = availableYears
                    .FirstOrDefault(sy => string.Equals(sy, schoolYear, StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrWhiteSpace(targetYear))
            {
                targetYear = availableYears.LastOrDefault();
            }

            if (string.IsNullOrWhiteSpace(targetYear))
            {
                targetYear = GetCurrentTerm().SchoolYear;
            }

            return new TermInfoResult
            {
                TargetSchoolYear = targetYear
            };
        }

        private static TermInfo BuildTermInfo(int startYear, int endYear, int semesterOrder)
        {
            semesterOrder = semesterOrder switch
            {
                < 1 => 1,
                > 3 => 3,
                _ => semesterOrder
            };

            var label = semesterOrder switch
            {
                1 => $"1st Semester {startYear}",
                2 => $"2nd Semester {startYear + 1}",
                _ => $"Mid Term {startYear + 1}"
            };

            return new TermInfo
            {
                SchoolYear = $"{startYear}-{endYear}",
                YearStart = startYear,
                SemesterOrder = semesterOrder,
                TermKey = $"{startYear}-{endYear}-{semesterOrder}",
                Label = label
            };
        }

        private static int ExtractSemesterOrder(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 1;
            }

            text = text.ToLowerInvariant();
            if (text.Contains("first") || text.Contains("1st"))
            {
                return 1;
            }

            if (text.Contains("second") || text.Contains("2nd"))
            {
                return 2;
            }

            if (text.Contains("mid"))
            {
                return 3;
            }

            var match = Regex.Match(text, @"-(\d)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var order))
            {
                return Math.Clamp(order, 1, 3);
            }

            return 1;
        }

        private static TermInfo GetCurrentTerm()
        {
            var now = DateTime.UtcNow;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            var endYear = startYear + 1;
            var semOrder = now.Month switch
            {
                >= 6 and <= 10 => 1,
                >= 11 or <= 3 => 2,
                _ => 3
            };

            return BuildTermInfo(startYear, endYear, semOrder);
        }

        private static TermInfo GetPreviousTerm(TermInfo current)
        {
            if (current.SemesterOrder > 1)
            {
                return BuildTermInfo(current.YearStart, current.YearStart + 1, current.SemesterOrder - 1);
            }

            var previousStart = current.YearStart - 1;
            return BuildTermInfo(previousStart, previousStart + 1, 2);
        }

        private static bool MatchesTermKey(string rawSemester, string schoolYear, string termKey)
        {
            var parsed = ParseTerm(rawSemester, schoolYear);
            return parsed != null && string.Equals(parsed.TermKey, termKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeYearLevel(string yearLevel)
        {
            if (string.IsNullOrWhiteSpace(yearLevel))
            {
                return "N/A";
            }

            if (int.TryParse(yearLevel, out var numeric))
            {
                return numeric switch
                {
                    1 => "1st Year",
                    2 => "2nd Year",
                    3 => "3rd Year",
                    4 => "4th Year",
                    _ => $"{numeric}th Year"
                };
            }

            return yearLevel;
        }

        private class TermInfo
        {
            public string SchoolYear { get; set; }
            public int YearStart { get; set; }
            public int SemesterOrder { get; set; }
            public string TermKey { get; set; }
            public string Label { get; set; }
        }

        private class TermInfoResult
        {
            public string TargetSchoolYear { get; set; }
        }

        private class StudentSnapshot
        {
            public int StudentId { get; set; }
            public string Program { get; set; }
            public string YearLevel { get; set; }
            public List<Grade> Grades { get; set; } = new();
        }
    }
}
