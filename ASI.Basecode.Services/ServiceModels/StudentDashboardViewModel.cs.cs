using System;

namespace ASI.Basecode.Services.ServiceModels
{
    public class StudentDashboardViewModel
    {
        // Header / identity
        public string StudentName { get; set; } = "Student";
        public string Program { get; set; } = "";
        public string YearLevel { get; set; } = "";

        // KPIs
        public decimal? CumulativeGwa { get; set; }
        public int CurrentTermUnits { get; set; }
        public bool IsDeanListEligible { get; set; }

        // Context
        public string CurrentSchoolYear { get; set; } = "";
        public string CurrentSemesterName { get; set; } = "";

        // Line chart (4 points: Prelims, Midterms, SemiFinals, Finals)
        public decimal?[] Sem1Series { get; set; } = Array.Empty<decimal?>();
        public decimal?[] Sem2Series { get; set; } = Array.Empty<decimal?>();

        public string Sem1Label { get; set; } = "1st Semester";
        public string Sem2Label { get; set; } = "2nd Semester";

        // For your donut math in the Razor
        public decimal GwaScaleMax { get; set; } = 5.00m;
        public decimal GwaBest { get; set; } = 1.00m;
    }
}
