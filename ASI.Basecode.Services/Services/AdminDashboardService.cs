using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ASI.Basecode.Data;
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
            var nowTerm = GetCurrentTerm();
            var prevTerm = GetPreviousTerm(nowTerm);

            var totalStudents = await _ctx.Students.CountAsync();
            var totalTeachers = await _ctx.Teachers.CountAsync();
            var currentCourses = await CountCoursesForTermAsync(nowTerm.TermKey);
            var previousCourses = await CountCoursesForTermAsync(prevTerm.TermKey);

            var currentEnrollment = await CountEnrolledStudentsAsync(nowTerm.TermKey);
            var previousEnrollment = await CountEnrolledStudentsAsync(prevTerm.TermKey);

            return new DashboardSummaryModel
            {
                TotalStudents = totalStudents,
                StudentsChangePercent = ComputeChangePercent(previousEnrollment, currentEnrollment),
                TotalTeachers = totalTeachers,
                TeachersChangePercent = 0,
                ActiveCourses = currentCourses,
                ActiveCoursesChangePercent = ComputeChangePercent(previousCourses, currentCourses)
            };
        }

        public async Task<IList<EnrollmentTrendPointModel>> GetEnrollmentTrendAsync(int maxPoints = 8)
        {
            var gradeProjections = await _ctx.Grades
                .Where(g => g.AssignedCourse != null && g.AssignedCourse.Semester != null)
                .Select(g => new
                {
                    g.StudentId,
                    g.AssignedCourse.Semester
                })
                .AsNoTracking()
                .ToListAsync();

            var trend = gradeProjections
                .Select(x => ParseTerm(x.Semester))
                .Where(t => t != null)
                .GroupBy(t => t.TermKey)
                .Select(g => new EnrollmentTrendPointModel
                {
                    TermKey = g.Key,
                    Label = g.First().Label,
                    YearStart = g.First().YearStart,
                    SemesterOrder = g.First().SemesterOrder,
                    StudentCount = gradeProjections
                        .Where(p => MatchesTermKey(p.Semester, g.Key))
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

        public async Task<IList<string>> GetAvailableSchoolYearsAsync()
        {
            var semesters = await _ctx.AssignedCourses
                .Where(ac => ac.Semester != null)
                .Select(ac => ac.Semester)
                .AsNoTracking()
                .ToListAsync();

            return semesters
                .Select(ParseTerm)
                .Where(t => t != null)
                .Select(t => t.SchoolYear)
                .Distinct()
                .OrderBy(sy => sy)
                .ToList();
        }

        public async Task<DashboardYearDetailModel> GetYearDetailAsync(string schoolYear = null, string termKey = null)
        {
            var termInfo = await BuildTermInfoForYearAsync(schoolYear);
            var targetYear = termInfo.TargetSchoolYear;

            var gradeEntries = await _ctx.Grades
                .Where(g => g.AssignedCourse != null && g.AssignedCourse.Semester != null)
                .Include(g => g.Student)
                .Include(g => g.AssignedCourse)
                .AsNoTracking()
                .ToListAsync();

            var parsedEntries = gradeEntries
                .Select(g => new
                {
                    Grade = g,
                    Term = ParseTerm(g.AssignedCourse.Semester)
                })
                .Where(x => x.Term != null && string.Equals(x.Term.SchoolYear, targetYear, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var termOptions = parsedEntries
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

            var selectedTermKeyForReturn = normalizedTermKey ?? string.Empty;
            var selectedTermLabel = termOptions
                .FirstOrDefault(t => string.Equals(t.TermKey ?? string.Empty, selectedTermKeyForReturn, StringComparison.OrdinalIgnoreCase))
                ?.Label ?? wholeYearLabel;

            var scopedEntries = !string.IsNullOrEmpty(normalizedTermKey)
                ? parsedEntries.Where(x => string.Equals(x.Term.TermKey, normalizedTermKey, StringComparison.OrdinalIgnoreCase)).ToList()
                : parsedEntries;

            var yearStudentGroups = parsedEntries
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

            var totalUniqueStudents = yearStudentGroups.Count;

            var programShares = yearStudentGroups
                .GroupBy(s => s.Program)
                .Select(g => new ProgramShareModel
                {
                    Program = g.Key,
                    StudentCount = g.Count(),
                    SharePercent = totalUniqueStudents == 0 ? 0 : Math.Round((decimal)g.Count() / totalUniqueStudents * 100m, 1)
                })
                .OrderByDescending(p => p.StudentCount)
                .ToList();

            var yearLevelSeries = yearStudentGroups
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

            var averageGpa = termOptions.Count > 0
                ? termOptions.Select(option => new GpaTrendPointModel
                {
                    TermKey = option.TermKey,
                    Label = option.Label,
                    AverageGpa = ComputeAverageGwa(parsedEntries
                        .Where(x => string.Equals(x.Term.TermKey, option.TermKey, StringComparison.OrdinalIgnoreCase))
                        .Select(x => ExtractFinalScore(x.Grade)))
                }).ToList()
                : new List<GpaTrendPointModel>();

            var passFailSource = scopedEntries.Any() ? scopedEntries : parsedEntries;

            var passFail = passFailSource
                .Where(x => x.Grade.Student != null)
                .GroupBy(x => x.Grade.Student.Program ?? "N/A")
                .Select(g =>
                {
                    var finalGrades = g
                        .Select(x => ExtractFinalScore(x.Grade))
                        .Where(v => v.HasValue)
                        .Select(v => v.Value)
                        .ToList();

                    var passed = finalGrades.Count(val => val >= 75m);
                    var failed = finalGrades.Count(val => val < 75m);
                    var total = passed + failed;

                    return new PassFailRateModel
                    {
                        Program = g.Key,
                        Passed = passed,
                        Failed = failed,
                        PassRate = total == 0 ? 0 : Math.Round((decimal)passed / total * 100m, 1),
                        FailRate = total == 0 ? 0 : Math.Round((decimal)failed / total * 100m, 1),
                        TermKey = selectedTermKeyForReturn,
                        TermLabel = selectedTermLabel
                    };
                })
                .OrderByDescending(p => p.PassRate)
                .ToList();

            return new DashboardYearDetailModel
            {
                SchoolYear = targetYear,
                ProgramShares = programShares,
                YearLevelSeries = yearLevelSeries,
                AverageGpa = averageGpa,
                PassFailRates = passFail,
                TermOptions = termOptions,
                SelectedTermKey = selectedTermKeyForReturn
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

        private static decimal? ExtractFinalScore(Data.Models.Grade grade)
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

        private async Task<int> CountCoursesForTermAsync(string termKey)
        {
            if (string.IsNullOrEmpty(termKey))
            {
                return await _ctx.AssignedCourses.CountAsync();
            }

            var semesters = await _ctx.AssignedCourses
                .Where(ac => ac.Semester != null)
                .Select(ac => ac.Semester)
                .ToListAsync();

            return semesters.Count(sem => MatchesTermKey(sem, termKey));
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

            var termEnrollments = await _ctx.Grades
                .Where(g => g.AssignedCourse != null && g.AssignedCourse.Semester != null)
                .Select(g => new { g.StudentId, g.AssignedCourse.Semester })
                .ToListAsync();

            return termEnrollments
                .Where(e => MatchesTermKey(e.Semester, termKey))
                .Select(e => e.StudentId)
                .Distinct()
                .Count();
        }

        private static decimal ComputeAverageGwa(IEnumerable<decimal?> grades)
        {
            var mapped = grades
                .Where(g => g.HasValue)
                .Select(g => MapPercentToGwa(g.Value))
                .ToList();

            if (!mapped.Any())
            {
                return 0m;
            }

            return Math.Round(mapped.Average(), 2);
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

        private async Task<TermInfoResult> BuildTermInfoForYearAsync(string schoolYear)
        {
            var available = await GetAvailableSchoolYearsAsync();
            var targetYear = string.IsNullOrWhiteSpace(schoolYear)
                ? available.LastOrDefault()
                : available.FirstOrDefault(sy => string.Equals(sy, schoolYear, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(targetYear) && available.Any())
            {
                targetYear = available.Last();
            }

            return new TermInfoResult
            {
                TargetSchoolYear = targetYear ?? GetCurrentTerm().SchoolYear
            };
        }

        private static string NormalizeYearLevel(string yearLevel)
        {
            if (string.IsNullOrWhiteSpace(yearLevel)) return "N/A";

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

        private static TermInfo ParseTerm(string rawSemester)
        {
            if (string.IsNullOrWhiteSpace(rawSemester))
            {
                return null;
            }

            var trimmed = rawSemester.Trim();

            var directMatch = Regex.Match(trimmed, @"(20\d{2})\s*[-/]\s*(20\d{2})\s*-\s*(\d)");
            if (directMatch.Success)
            {
                var start = int.Parse(directMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var end = int.Parse(directMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                var semOrder = int.Parse(directMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                return BuildTermInfo(start, end, semOrder);
            }

            var syMatch = Regex.Match(trimmed, @"(20\d{2})\D+(20\d{2})");
            if (syMatch.Success)
            {
                var start = int.Parse(syMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var end = int.Parse(syMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                var semOrder = ExtractSemesterOrder(trimmed);
                return BuildTermInfo(start, end, semOrder);
            }

            return null;
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
            if (text.Contains("first") || text.Contains("1st")) return 1;
            if (text.Contains("second") || text.Contains("2nd")) return 2;
            if (text.Contains("mid")) return 3;

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

            var prevStart = current.YearStart - 1;
            return BuildTermInfo(prevStart, prevStart + 1, 2);
        }

        private static bool MatchesTermKey(string rawSemester, string termKey)
        {
            var parsed = ParseTerm(rawSemester);
            return parsed != null && string.Equals(parsed.TermKey, termKey, StringComparison.OrdinalIgnoreCase);
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
            public List<Data.Models.Grade> Grades { get; set; } = new();
        }
    }
}
