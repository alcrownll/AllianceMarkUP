using System.Collections.Generic;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models
{
    public class AdminReportsViewModel
    {
        public ReportsDashboardModel Dashboard { get; set; }
        public IList<string> SchoolYears { get; set; } = new List<string>();
        public string SelectedSchoolYear { get; set; }
        public string SelectedTermKey { get; set; }
        public int? SelectedTeacherId { get; set; }
        public int? SelectedStudentId { get; set; }
        public int? SelectedStudentProgramId { get; set; }
        public int? SelectedStudentCourseId { get; set; }
    }
}