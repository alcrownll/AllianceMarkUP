using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class TeacherDashboardViewModel
    {
        public string TeacherName { get; set; }

        // KPIs
        public int TotalCourses { get; set; }
        public int TotalStudents { get; set; }

        // Donut (0..1)
        public decimal GradedPct { get; set; }

        // Lists per program
        public ProgramCourses IT { get; set; } = new();
        public ProgramCourses CS { get; set; } = new();

        // Chart summary
        public ProgramSummary Summary { get; set; } = new();
    }

    public class ProgramCourses
    {
        public List<CourseItem> Courses { get; set; } = new();
        public int StudentsTotal { get; set; }
    }

    public class CourseItem
    {
        public string Code { get; set; }          // e.g., CourseCode
        public string Title { get; set; }         // we map from Course.Description in the service
        public int Students { get; set; }         // distinct Student count from Grades
    }

    public class ProgramSummary
    {
        public int IT_Courses { get; set; }
        public int CS_Courses { get; set; }
        public int IT_Students { get; set; }
        public int CS_Students { get; set; }
    }
}
