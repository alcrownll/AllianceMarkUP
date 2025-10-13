using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ITeacherDashboardService
    {
        Task<TeacherDashboardViewModel> BuildAsync(string idNumber);
    }
}
