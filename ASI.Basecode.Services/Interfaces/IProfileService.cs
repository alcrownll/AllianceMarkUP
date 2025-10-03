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
        int GetCurrentStudentId();

        // Student
        Task<StudentProfileViewModel> GetStudentProfileAsync();
        Task UpdateStudentProfileAsync(StudentProfileViewModel input);

        // Teacher (kept for parity; still resolves current user internally)
        Task<TeacherProfileViewModel> GetTeacherProfileAsync();
        Task UpdateTeacherProfileAsync(TeacherProfileViewModel input);
    }
}
