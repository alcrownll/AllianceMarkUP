using System.Collections.Generic;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<DashboardSummaryModel> GetSummaryAsync();
        Task<IList<EnrollmentTrendPointModel>> GetEnrollmentTrendAsync(int maxPoints = 8);
        Task<IList<string>> GetAvailableSchoolYearsAsync();
        Task<DashboardYearDetailModel> GetYearDetailAsync(string schoolYear = null, string termKey = null);
    }
}
