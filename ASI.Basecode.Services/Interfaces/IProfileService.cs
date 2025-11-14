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
        /// <summary>
        /// User updates their own student profile (My Activity only for that user).
        /// </summary>
        Task UpdateStudentProfileAsync(int userId, StudentProfileViewModel input);

        /// <summary>
        /// Admin updates a student's profile (My Activity for admin + Updates for student).
        /// </summary>
        Task UpdateStudentProfileByAdminAsync(int adminUserId, int targetUserId, StudentProfileViewModel input);

        Task<TeacherProfileViewModel> GetTeacherProfileAsync(int userId);
        /// <summary>
        /// User updates their own teacher profile (My Activity only for that user).
        /// </summary>
        Task UpdateTeacherProfileAsync(int userId, TeacherProfileViewModel input);

        /// <summary>
        /// Admin updates a teacher's profile (My Activity for admin + Updates for teacher).
        /// </summary>
        Task UpdateTeacherProfileByAdminAsync(int adminUserId, int targetUserId, TeacherProfileViewModel input);

        Task<ProfileViewModel> GetAdminProfileAsync(int userId);
        Task UpdateAdminProfileAsync(int userId, ProfileViewModel input);
    }
}
