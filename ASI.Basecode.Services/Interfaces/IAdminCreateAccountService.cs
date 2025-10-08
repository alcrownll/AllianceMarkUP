using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminCreateAccountService
    {
        (byte[] Content, string ContentType, string FileName) GenerateStudentsTemplate();
        Task<ImportResult> ImportStudentsAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct);

        (byte[] Content, string ContentType, string FileName) GenerateTeachersTemplate();
        Task<ImportResult> ImportTeachersAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct);

        Task<(int UserId, string IdNumber)> CreateSingleStudentAsync(StudentProfileViewModel vm, ImportUserDefaults defaults, CancellationToken ct);

        Task<(int UserId, string IdNumber)> CreateSingleTeacherAsync(TeacherProfileViewModel vm, ImportUserDefaults defaults, CancellationToken ct);
    }
}
