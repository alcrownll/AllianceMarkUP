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
        public DashboardYearDetailModel YearDetail { get; set; }
    }
}
