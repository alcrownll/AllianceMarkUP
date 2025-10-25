using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

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

        private readonly INotificationService _notificationService;

        public TeacherCourseService(
            AsiBasecodeDBContext ctx,
            IAssignedCourseRepository assignedCourseRepository,
            IGradeRepository gradeRepository,
            IClassScheduleRepository classScheduleRepository,
            IStudentRepository studentRepository,
            IUnitOfWork unitOfWork,
            INotificationService notificationService)  // Injecting NotificationService
        {
            _ctx = ctx;
            _assignedCourseRepository = assignedCourseRepository;
            _gradeRepository = gradeRepository;
            _classScheduleRepository = classScheduleRepository;
            _studentRepository = studentRepository;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;  // Assigning to field
        }

        public async Task<List<TeacherCourseViewModel>> GetTeacherAssignedCoursesAsync(int teacherId, string semester = null)
        {
            var currentSemester = semester ?? GetCurrentSemester();

            var assignedCourses = await _assignedCourseRepository.GetAssignedCourses()
                .Where(ac => ac.TeacherId == teacherId)
                .Where(ac => string.IsNullOrEmpty(currentSemester) || ac.Semester == currentSemester)
                .Include(ac => ac.Course)
                .Include(ac => ac.Program)
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
                    Section = DetermineSection(ac.Program?.ProgramCode),
                    Course = ac.Program?.ProgramCode,
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
                Remarks = CalculateRemarks(g.Prelims, g.Midterm, g.SemiFinal, g.Final)
            }).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
        }

        public async Task<List<TeacherClassScheduleViewModel>> GetTeacherClassSchedulesAsync(int teacherId, string semester = null)
        {
            var currentSemester = semester ?? GetCurrentSemester();

            var assignedCourses = await _assignedCourseRepository.GetAssignedCourses()
                .Where(ac => ac.TeacherId == teacherId)
                .Where(ac => string.IsNullOrEmpty(currentSemester) || ac.Semester == currentSemester)
                .Include(ac => ac.Course)
                .Include(ac => ac.Program)
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
                    Section = DetermineSection(ac.Program?.ProgramCode),
                    Course = ac.Program?.ProgramCode,
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
                // List of students whose grades have been updated
                List<int> affectedStudentIds = new List<int>();

                foreach (var gradeUpdate in grades)
                {
                    var existingGrade = await _ctx.Grades
                        .FirstOrDefaultAsync(g => g.GradeId == gradeUpdate.GradeId);

                    if (existingGrade != null)
                    {
                        // Check and update grades, and mark which students were affected
                        if (gradeUpdate.Prelims.HasValue)
                        {
                            existingGrade.Prelims = gradeUpdate.Prelims;
                            affectedStudentIds.Add(existingGrade.StudentId);
                        }
                        if (gradeUpdate.Midterm.HasValue)
                        {
                            existingGrade.Midterm = gradeUpdate.Midterm;
                            affectedStudentIds.Add(existingGrade.StudentId);
                        }
                        if (gradeUpdate.SemiFinal.HasValue)
                        {
                            existingGrade.SemiFinal = gradeUpdate.SemiFinal;
                            affectedStudentIds.Add(existingGrade.StudentId);
                        }
                        if (gradeUpdate.Final.HasValue)
                        {
                            existingGrade.Final = gradeUpdate.Final;
                            affectedStudentIds.Add(existingGrade.StudentId);
                        }

                        _gradeRepository.UpdateGrade(existingGrade);
                    }
                }

                await _unitOfWork.SaveChangesAsync();

                // After updating grades, notify students
                foreach (var studentId in affectedStudentIds.Distinct())
                {
                    var student = await _studentRepository.GetByIdAsync(studentId); // Get student details
                    if (student != null)
                    {
                        // Notify the student
                        _notificationService.AddNotification(
                            student.UserId, // Assuming the student has a UserId property
                            "Grade updated",
                            $"Your grade for a course has been uploaded."
                        );
                    }
                }

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
                .Include(ac => ac.Program)
                .Select(ac => ac.Program.ProgramCode)
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
                var searchNameLower = searchName.ToLower();
                gradesQuery = gradesQuery.Where(g => 
                    g.Student.User.FirstName.ToLower().Contains(searchNameLower) || 
                    g.Student.User.LastName.ToLower().Contains(searchNameLower));
            }

            if (!string.IsNullOrEmpty(searchId))
            {
                gradesQuery = gradesQuery.Where(g => g.Student.User.IdNumber.StartsWith(searchId));
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
                
                // Calculate average grades across all records for this student
                var studentGradeRecords = group.ToList();
                var firstGrade = studentGradeRecords.First(); // Use first record for non-grade fields
                
                // Calculate averages for each grade component
                var prelimsValues = studentGradeRecords.Where(g => g.Prelims.HasValue).Select(g => g.Prelims.Value).ToList();
                var midtermValues = studentGradeRecords.Where(g => g.Midterm.HasValue).Select(g => g.Midterm.Value).ToList();
                var semiFinalValues = studentGradeRecords.Where(g => g.SemiFinal.HasValue).Select(g => g.SemiFinal.Value).ToList();
                var finalValues = studentGradeRecords.Where(g => g.Final.HasValue).Select(g => g.Final.Value).ToList();
                
                var avgPrelims = prelimsValues.Any() ? (decimal?)Math.Round(prelimsValues.Average(), 2) : null;
                var avgMidterm = midtermValues.Any() ? (decimal?)Math.Round(midtermValues.Average(), 2) : null;
                var avgSemiFinal = semiFinalValues.Any() ? (decimal?)Math.Round(semiFinalValues.Average(), 2) : null;
                var avgFinal = finalValues.Any() ? (decimal?)Math.Round(finalValues.Average(), 2) : null;
                
                // Calculate weighted GPA and determine remarks based on student's performance
                var remarks = CalculateRemarks(avgPrelims, avgMidterm, avgSemiFinal, avgFinal);

                result.Add(new StudentGradeViewModel
                {
                    StudentId = student.StudentId,
                    GradeId = firstGrade.GradeId,
                    AssignedCourseId = firstGrade.AssignedCourseId,
                    IdNumber = student?.User?.IdNumber ?? "",
                    LastName = student?.User?.LastName ?? "",
                    FirstName = student?.User?.FirstName ?? "",
                    CourseYear = $"{student?.Program} {student?.YearLevel}",
                    Gender = student?.User?.UserProfile?.Gender ?? "",
                    Prelims = avgPrelims,
                    Midterm = avgMidterm,
                    SemiFinal = avgSemiFinal,
                    Final = avgFinal,
                    Remarks = remarks
                });
            }

            return result.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
        }

        public string GetCurrentSemesterName()
        {
            return GetCurrentSemester();
        }



        #region Private Helper Methods

        private string CalculateRemarks(decimal? prelims, decimal? midterm, decimal? semiFinal, decimal? final)
        {
            // Calculate weighted average similar to AdminDashboardService and AdminReportsService
            var components = new (decimal? Score, decimal Weight)[]
            {
                (prelims, 0.3m),     // 30%
                (midterm, 0.3m),     // 30%
                (semiFinal, 0.2m),   // 20%
                (final, 0.2m)        // 20%
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
                return "INCOMPLETE";
            }

            var gpa = Math.Round(weightedTotal / weightSum, 2);
            
            // Determine pass/fail based on GPA (assuming 3.0 is passing grade)
            return gpa <= 3.0m ? "PASSED" : "FAILED";
        }

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
            // Retrieve semester from AssignedCourses table
            var semester = _assignedCourseRepository.GetAssignedCourses()
                .Select(ac => ac.Semester)
                .Where(s => !string.IsNullOrEmpty(s))
                .FirstOrDefault();
            
            return semester ?? "--"; // Default fallback if no semester found
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

        #region Excel Upload Methods

        public ExcelUploadResultModel ParseExcelFile(byte[] fileBytes)
        {
            var result = new ExcelUploadResultModel();
            var excelGrades = new List<ExcelGradeUploadModel>();

            try
            {
                using var stream = new MemoryStream(fileBytes);
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Expected columns: ID Number, Last Name, First Name, Prelims, Midterm, Semi-Final, Final
                var rows = worksheet.RowsUsed().Skip(1); // Skip header row

                foreach (var row in rows)
                {
                    try
                    {
                        var excelGrade = new ExcelGradeUploadModel
                        {
                            IdNumber = row.Cell(1).GetString().Trim(),
                            LastName = row.Cell(2).GetString().Trim(),
                            FirstName = row.Cell(3).GetString().Trim()
                        };

                        // Parse numeric grades (allow empty/null values)
                        if (!string.IsNullOrWhiteSpace(row.Cell(4).GetString()))
                        {
                            if (decimal.TryParse(row.Cell(4).GetString(), out decimal prelims) && prelims >= 1.0m && prelims <= 5.0m)
                                excelGrade.Prelims = prelims;
                            else
                                result.Errors.Add($"Row {row.RowNumber()}: Invalid Prelims grade '{row.Cell(4).GetString()}' for {excelGrade.IdNumber}");
                        }

                        if (!string.IsNullOrWhiteSpace(row.Cell(5).GetString()))
                        {
                            if (decimal.TryParse(row.Cell(5).GetString(), out decimal midterm) && midterm >= 1.0m && midterm <= 5.0m)
                                excelGrade.Midterm = midterm;
                            else
                                result.Errors.Add($"Row {row.RowNumber()}: Invalid Midterm grade '{row.Cell(5).GetString()}' for {excelGrade.IdNumber}");
                        }

                        if (!string.IsNullOrWhiteSpace(row.Cell(6).GetString()))
                        {
                            if (decimal.TryParse(row.Cell(6).GetString(), out decimal semiFinal) && semiFinal >= 1.0m && semiFinal <= 5.0m)
                                excelGrade.SemiFinal = semiFinal;
                            else
                                result.Errors.Add($"Row {row.RowNumber()}: Invalid Semi-Final grade '{row.Cell(6).GetString()}' for {excelGrade.IdNumber}");
                        }

                        if (!string.IsNullOrWhiteSpace(row.Cell(7).GetString()))
                        {
                            if (decimal.TryParse(row.Cell(7).GetString(), out decimal final) && final >= 1.0m && final <= 5.0m)
                                excelGrade.Final = final;
                            else
                                result.Errors.Add($"Row {row.RowNumber()}: Invalid Final grade '{row.Cell(7).GetString()}' for {excelGrade.IdNumber}");
                        }

                        if (string.IsNullOrWhiteSpace(excelGrade.IdNumber))
                        {
                            result.Errors.Add($"Row {row.RowNumber()}: ID Number is required");
                            continue;
                        }

                        excelGrades.Add(excelGrade);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {row.RowNumber()}: Error processing row - {ex.Message}");
                    }
                }

                result.ProcessedGrades = excelGrades;
                result.ProcessedCount = excelGrades.Count;
                result.ErrorCount = result.Errors.Count;
                result.Success = result.Errors.Count == 0;
                result.Message = result.Success 
                    ? $"Successfully parsed {result.ProcessedCount} records" 
                    : $"Parsed {result.ProcessedCount} records with {result.ErrorCount} errors";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error reading Excel file: {ex.Message}";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        public async Task<ExcelUploadResultModel> ProcessExcelGradeUploadAsync(int assignedCourseId, List<ExcelGradeUploadModel> excelGrades)
        {
            var result = new ExcelUploadResultModel();
            var successCount = 0;
            var errorCount = 0;

            try
            {
                // Get all students and grades for this assigned course
                var studentsWithGrades = await _ctx.Grades
                    .Include(g => g.Student)
                    .ThenInclude(s => s.User)
                    .Where(g => g.AssignedCourseId == assignedCourseId)
                    .ToListAsync();

                foreach (var excelGrade in excelGrades)
                {
                    try
                    {
                        // Find matching student by ID Number
                        var gradeRecord = studentsWithGrades
                            .FirstOrDefault(g => g.Student.User.IdNumber.Equals(excelGrade.IdNumber.Trim(), StringComparison.OrdinalIgnoreCase));

                        if (gradeRecord == null)
                        {
                            result.Errors.Add($"Student with ID Number '{excelGrade.IdNumber}' not found in this course");
                            errorCount++;
                            continue;
                        }

                        // Validate student name matches (optional but recommended for data integrity)
                        var studentFullName = $"{gradeRecord.Student.User.FirstName} {gradeRecord.Student.User.LastName}".ToLower();
                        var excelFullName = $"{excelGrade.FirstName} {excelGrade.LastName}".ToLower();
                        
                        if (!studentFullName.Contains(excelGrade.FirstName.ToLower()) || 
                            !studentFullName.Contains(excelGrade.LastName.ToLower()))
                        {
                            result.Errors.Add($"Name mismatch for ID '{excelGrade.IdNumber}': Expected '{studentFullName}', Excel has '{excelFullName}'");
                            errorCount++;
                            continue;
                        }

                        // Update grades (only update non-null values)
                        var updated = false;
                        if (excelGrade.Prelims.HasValue && gradeRecord.Prelims != excelGrade.Prelims)
                        {
                            gradeRecord.Prelims = excelGrade.Prelims;
                            updated = true;
                        }
                        if (excelGrade.Midterm.HasValue && gradeRecord.Midterm != excelGrade.Midterm)
                        {
                            gradeRecord.Midterm = excelGrade.Midterm;
                            updated = true;
                        }
                        if (excelGrade.SemiFinal.HasValue && gradeRecord.SemiFinal != excelGrade.SemiFinal)
                        {
                            gradeRecord.SemiFinal = excelGrade.SemiFinal;
                            updated = true;
                        }
                        if (excelGrade.Final.HasValue && gradeRecord.Final != excelGrade.Final)
                        {
                            gradeRecord.Final = excelGrade.Final;
                            updated = true;
                        }

                        if (updated)
                        {
                            _gradeRepository.UpdateGrade(gradeRecord);
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error processing student '{excelGrade.IdNumber}': {ex.Message}");
                        errorCount++;
                    }
                }

                // Save all changes to database
                if (successCount > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                }

                result.ProcessedCount = successCount;
                result.ErrorCount = errorCount;
                result.Success = errorCount == 0 && successCount > 0;
                result.Message = result.Success 
                    ? $"Successfully updated {successCount} student grades" 
                    : $"Updated {successCount} grades with {errorCount} errors";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Database error: {ex.Message}";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        #endregion
    }
}