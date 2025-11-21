using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels; // StudentGradesViewModel

namespace ASI.Basecode.Services.Interfaces
{
    public interface IStudentGradesService
    {
        Task<StudentGradesViewModel> BuildAsync(int userId, string schoolYear, string semester, CancellationToken ct = default);
    }
}
