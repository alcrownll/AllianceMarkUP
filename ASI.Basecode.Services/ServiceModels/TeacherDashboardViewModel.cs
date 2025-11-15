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

        // Dynamic programs list
        public List<ProgramData> Programs { get; set; } = new();

        // Chart summary (now dynamic)
        public List<ProgramSummaryItem> Summary { get; set; } = new();
    }

    public class ProgramData
    {
        public string ProgramCode { get; set; }           // e.g., "BSCS", "BSIT", "BSECE"
        public List<CourseItem> Courses { get; set; } = new();
        public int StudentsTotal { get; set; }
    }

    public class CourseItem
    {
        public string Code { get; set; }          // e.g., CourseCode
        public string Title { get; set; }         // map from Course.Description in the service
        public int Students { get; set; }         // distinct Student count from Grades
    }

    public class ProgramSummaryItem
    {
        public string ProgramCode { get; set; }
        public int Courses { get; set; }
        public int Students { get; set; }
    }
}
