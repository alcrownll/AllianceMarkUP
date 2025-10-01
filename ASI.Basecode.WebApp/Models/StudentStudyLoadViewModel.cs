using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Models
{
    public class StudentStudyLoadViewModel
    {
        public string StudentName { get; set; }
        public string Program { get; set; }
        public string YearLevel { get; set; }

        /// <summary>
        /// Term key stored in AssignedCourses.Semester, e.g. "2025-2026-1" or "2025-2026-2"
        /// </summary>
        public string SelectedTerm { get; set; }
        public List<TermItem> Terms { get; set; } = new();

        public List<StudentStudyLoadRow> Rows { get; set; } = new();
        public int TotalUnits => Rows.Sum(r => r.Units);

        public string SchoolYear =>
            string.IsNullOrWhiteSpace(SelectedTerm) || !SelectedTerm.Contains('-')
                ? ""
                : SelectedTerm.Split('-')[0] + " - " + SelectedTerm.Split('-')[1];

        public string SemesterText =>
            string.IsNullOrWhiteSpace(SelectedTerm) || !SelectedTerm.Contains('-')
                ? ""
                : (SelectedTerm.EndsWith("-1") ? "First Semester" :
                   SelectedTerm.EndsWith("-2") ? "Second Semester" : SelectedTerm);
    }

    public class TermItem
    {
        public string Value { get; set; }   // "YYYY-YYYY-1"
        public string Text { get; set; }   // "S.Y. 2025-2026 - First Semester"
        public bool Selected { get; set; }
    }

    public class StudentStudyLoadRow
    {
        public string EDPCode { get; set; }
        public string Subject { get; set; }       // CourseCode
        public string Description { get; set; }
        public string Instructor { get; set; }    // Teacher.User full name
        public int Units { get; set; }            // AssignedCourses.Units (or Lec+Lab)
        public string Type { get; set; }          // LEC/LAB
        public string Room { get; set; }
        public string DateTime { get; set; }      // e.g., "MWF (6:30 PM - 7:30 PM)"
    }
}
