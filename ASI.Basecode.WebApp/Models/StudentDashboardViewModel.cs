using System;

namespace ASI.Basecode.WebApp.Models
{
    public class StudentDashboardViewModel
    {
        // Display
        public string StudentName { get; set; }
        public string Program { get; set; }
        public string YearLevel { get; set; }

        // Core metrics (1.00 best, 5.00 worst)
        public decimal? CumulativeGwa { get; set; }
        public int CurrentTermUnits { get; set; }

        // Honors flag (1.00–1.70 inclusive)
        public bool IsDeanListEligible { get; set; }

        // Chart helpers
        public decimal GwaScaleMax => 5.00m;
        public decimal GwaBest => 1.00m;

        // Context
        public string CurrentSchoolYear { get; set; }    // e.g., "2025-2026"
        public string CurrentSemesterName { get; set; }  // e.g., "1st Semester"

        // Dynamic “Academic Highlights” (averaged across courses)
        // Each array holds: [Prelims, Midterm, SemiFinal, Final]
        public decimal?[] Sem1Series { get; set; }
        public decimal?[] Sem2Series { get; set; }
        public string Sem1Label { get; set; } = "1st Semester";
        public string Sem2Label { get; set; } = "2nd Semester";
    }
}
