using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IStudentDashboardService
    {
        /// <summary>
        /// Build the Student Dashboard VM for the student identified by IdNumber (session).
        /// </summary>
        Task<StudentDashboardViewModel> BuildAsync(string idNumber);
    }
}
