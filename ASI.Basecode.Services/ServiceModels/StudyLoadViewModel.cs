using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class StudyLoadViewModel
    {
        public string StudentName { get; set; }
        public string Program { get; set; }
        public string YearLevel { get; set; }
        public string Section { get; set; }  

        public string SelectedTerm { get; set; } // e.g., 
        public List<TermItem> Terms { get; set; } = new();

        public List<StudyLoadRow> Rows { get; set; } = new();
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
        public string Value { get; set; }  // "2025-2026-1"
        public string Text { get; set; }   // "S.Y. 2025-2026 - First Semester"
        public bool Selected { get; set; }
    }

    public class StudyLoadRow
    {
        public string EDPCode { get; set; }
        public string Subject { get; set; }       // CourseCode
        public string Description { get; set; }   // Course.Description
        public string Instructor { get; set; }    // Teacher.User full name
        public int Units { get; set; }            // AssignedCourses.Units (or computed)
        public string Type { get; set; }          // LEC / LAB
        public string Room { get; set; }          // If multiple rooms/times, show 1st or leave blank
        public string DateTime { get; set; }      // e.g., "MWF (6:30 PM - 7:30 PM)"
    }
}
