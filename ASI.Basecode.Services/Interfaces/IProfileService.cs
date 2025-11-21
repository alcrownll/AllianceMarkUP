using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IProfileService
    {
        int GetCurrentUserId();
        int GetCurrentTeacherId();

        Task<StudentProfileViewModel> GetStudentProfileAsync(int userId);

        Task UpdateStudentProfileAsync(int userId, StudentProfileViewModel input);

        Task UpdateStudentProfileByAdminAsync(int adminUserId, int targetUserId, StudentProfileViewModel input);
        Task<List<ProgramOption>> GetActiveProgramsAsync(CancellationToken ct = default);

        Task<TeacherProfileViewModel> GetTeacherProfileAsync(int userId);

        Task UpdateTeacherProfileAsync(int userId, TeacherProfileViewModel input);

        Task UpdateTeacherProfileByAdminAsync(int adminUserId, int targetUserId, TeacherProfileViewModel input);

        Task<ProfileViewModel> GetAdminProfileAsync(int userId);
        Task UpdateAdminProfileAsync(int userId, ProfileViewModel input);
    }
}
