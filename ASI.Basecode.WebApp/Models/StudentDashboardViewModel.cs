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

        // Honors flag (tweak to your policy)
        public bool IsDeanListEligible { get; set; }

        // Chart helpers
        public decimal GwaScaleMax => 5.00m;
        public decimal GwaBest => 1.00m;

        // Context
        public string CurrentSchoolYear { get; set; }    // e.g., "2025-2026"
        public string CurrentSemesterName { get; set; }  // e.g., "1st Semester"
    }
}
