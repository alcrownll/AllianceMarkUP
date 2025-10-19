using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public class AdminReportsService : IAdminReportsService
    {
        public Task<ReportsDashboardModel> GetDashboardAsync(string schoolYear = null, string termKey = null, int? highlightedTeacherId = null, int? highlightedStudentId = null)
        {
            return Task.FromResult(new ReportsDashboardModel());
        }

        public Task<TeacherDetailModel> GetTeacherDetailAsync(int teacherId, string schoolYear = null, string termKey = null)
        {
            return Task.FromResult(new TeacherDetailModel());
        }

        public Task<StudentAnalyticsModel> GetStudentAnalyticsAsync(int studentId, string schoolYear = null, string termKey = null)
        {
            return Task.FromResult(new StudentAnalyticsModel());
        }
    }
}
