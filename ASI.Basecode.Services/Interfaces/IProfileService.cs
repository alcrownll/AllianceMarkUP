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
        public int GetCurrentUserId();

        Task<StudentProfileViewModel> GetStudentProfileAsync(int userId);
        Task UpdateStudentProfileAsync(int userId, StudentProfileViewModel input);
        Task<TeacherProfileViewModel> GetTeacherProfileAsync(int userId);
        Task UpdateTeacherProfileAsync(int userId, TeacherProfileViewModel input);
    }
}
