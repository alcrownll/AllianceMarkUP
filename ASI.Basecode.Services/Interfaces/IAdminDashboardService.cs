using System.Collections.Generic;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminDashboardService
    {
        // 
        Task<DashboardSummaryModel> GetSummaryAsync(string schoolYear = null, string termKey = null, int? programId = null);
        Task<IList<EnrollmentTrendPointModel>> GetEnrollmentTrendAsync(int maxPoints = 8, int? programId = null);
        Task<IList<string>> GetAvailableSchoolYearsAsync();
        Task<AdminDashboardModel> GetYearDetailAsync(string schoolYear = null, string termKey = null, int? programId = null);
        Task<IList<ProgramOptionModel>> GetProgramOptionsAsync();
    }
}
