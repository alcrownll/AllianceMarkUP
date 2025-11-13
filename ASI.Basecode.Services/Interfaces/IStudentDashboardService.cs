using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IStudentDashboardService
    {
        Task<StudentDashboardViewModel> BuildAsync(string idNumber);
    }
}
