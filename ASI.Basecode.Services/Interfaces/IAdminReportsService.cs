using System.Collections.Generic;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminReportsService
    {
        Task<ReportsDashboardModel> GetDashboardAsync(string schoolYear = null, string termKey = null, int? highlightedTeacherId = null, int? highlightedStudentId = null, int? studentProgramId = null, int? studentCourseId = null);
        Task<TeacherDetailModel> GetTeacherDetailAsync(int teacherId, string schoolYear = null, string termKey = null);
        Task<StudentAnalyticsModel> GetStudentAnalyticsAsync(int studentId, string schoolYear = null, string termKey = null);
        Task<IList<StudentOptionModel>> GetStudentDirectoryAsync(string schoolYear = null, string termKey = null, int? programId = null, int? courseId = null);
    }
}
