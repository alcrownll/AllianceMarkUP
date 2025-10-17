using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class StudentGradesViewModel
    {
        public string StudentName { get; set; }
        public string Program { get; set; }
        public string Department { get; set; }
        public string YearLevel { get; set; }
        public string Semester { get; set; }
        public string SchoolYear { get; set; }
        public decimal? Gpa { get; set; }
        public IList<string> AvailableSchoolYears { get; set; } = new List<string>();
        public IList<string> AvailableSemesters { get; set; } = new List<string>();
        public string SelectedSchoolYear { get; set; }
        public string SelectedSemester { get; set; }
        public IList<StudentGradeRowViewModel> Grades { get; set; } = new List<StudentGradeRowViewModel>();
    }

    public class StudentGradeRowViewModel
    {
        public string SubjectCode { get; set; }
        public string Description { get; set; }
        public string Instructor { get; set; }
        public string Type { get; set; }
        public int Units { get; set; }
        public decimal? Prelims { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? SemiFinal { get; set; }
        public decimal? Final { get; set; }
        public string Remarks { get; set; }
        public string Semester { get; set; }
        public string SchoolYear { get; set; }
    }
}
