using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminCreateAccountService
    {
        Task<(int UserId, string IdNumber)> CreateSingleStudentAsync(
            int adminUserId,
            StudentProfileViewModel vm,
            ImportUserDefaults defaults,
            CancellationToken ct);

        Task<(int UserId, string IdNumber)> CreateSingleTeacherAsync(
            int adminUserId,
            TeacherProfileViewModel vm,
            ImportUserDefaults defaults,
            CancellationToken ct);

        (byte[] Content, string ContentType, string FileName) GenerateStudentsTemplate();
        (byte[] Content, string ContentType, string FileName) GenerateTeachersTemplate();

        Task<ImportResult> ImportStudentsAsync(
            int adminUserId,
            IFormFile file,
            ImportUserDefaults defaults,
            CancellationToken ct);

        Task<ImportResult> ImportTeachersAsync(
            int adminUserId,
            IFormFile file,
            ImportUserDefaults defaults,
            CancellationToken ct);
    }
}
