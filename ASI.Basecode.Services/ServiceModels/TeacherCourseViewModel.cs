using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class TeacherCourseViewModel
    {
        public int AssignedCourseId { get; set; }
        public string EDPCode { get; set; }
        public string Course { get; set; }         // CourseCode
        public string Description { get; set; }    // Course Description
        public string Type { get; set; }           // LEC / LAB
        public int Units { get; set; }
        public string DateTime { get; set; }       // Formatted schedule
        public string Room { get; set; }
        public string Section { get; set; }        // Derived from program/year
        public string Program { get; set; }        // Program (BSCS, BSIT, etc.)
        public string Semester { get; set; }       
        public int StudentCount { get; set; }      // Number of enrolled students
    }

    public class TeacherClassScheduleViewModel
    {
        public int AssignedCourseId { get; set; }
        public string EDPCode { get; set; }
        public string Course { get; set; }
        public string Type { get; set; }
        public int Units { get; set; }
        public string DateTime { get; set; }
        public string Room { get; set; }
        public string Section { get; set; }
        public string Program { get; set; }
        public List<StudentGradeViewModel> Students { get; set; } = new List<StudentGradeViewModel>();
    }

    public class StudentGradeViewModel
    {
        public int StudentId { get; set; }
        public int GradeId { get; set; }
        public int AssignedCourseId { get; set; }
        public string IdNumber { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public string ProgramYear { get; set; }
        public string Gender { get; set; }
        
        [Range(1.0, 5.0, ErrorMessage = "Grade must be between 1.0 and 5.0")]
        public decimal? Prelims { get; set; }
        
        [Range(1.0, 5.0, ErrorMessage = "Grade must be between 1.0 and 5.0")]
        public decimal? Midterm { get; set; }
        
        [Range(1.0, 5.0, ErrorMessage = "Grade must be between 1.0 and 5.0")]
        public decimal? SemiFinal { get; set; }
        
        [Range(1.0, 5.0, ErrorMessage = "Grade must be between 1.0 and 5.0")]
        public decimal? Final { get; set; }
        
        public string Remarks { get; set; }
        public bool IsReadOnly { get; set; } = false;
    }

    public class StudentGradeUpdateModel
    {
        public int GradeId { get; set; }
        public int StudentId { get; set; }
        public int AssignedCourseId { get; set; }
        public decimal? Prelims { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? SemiFinal { get; set; }
        public decimal? Final { get; set; }
        // Note: Remarks is calculated from grades, not stored in database
    }

    public class TeacherCourseFilterModel
    {
        public string Program { get; set; }
        public int? YearLevel { get; set; }
        public string SearchName { get; set; }
        public string SearchIdNumber { get; set; }
        public string Semester { get; set; }
        public string ExamType { get; set; }
    }

    public class ExcelGradeUploadModel
    {
        public string IdNumber { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public decimal? Prelims { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? SemiFinal { get; set; }
        public decimal? Final { get; set; }
    }

    public class ExcelUploadResultModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ExcelGradeUploadModel> ProcessedGrades { get; set; } = new List<ExcelGradeUploadModel>();
    }
}