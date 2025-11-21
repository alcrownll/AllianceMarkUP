using System.Collections.Generic;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models
{
    public class AdminDashboardViewModel
    {
        public DashboardSummaryModel Summary { get; set; }
        public IList<EnrollmentTrendPointModel> EnrollmentTrend { get; set; } = new List<EnrollmentTrendPointModel>();
        public IList<string> SchoolYears { get; set; } = new List<string>();
        public string SelectedSchoolYear { get; set; }
        public AdminDashboardModel YearDetail { get; set; }
        public IList<ProgramOptionModel> Programs { get; set; } = new List<ProgramOptionModel>();
        public int? SelectedProgramId { get; set; }
        public IList<ReportTermOptionModel> TermOptions { get; set; } = new List<ReportTermOptionModel>();
        public string SelectedTermKey { get; set; }
    }
}