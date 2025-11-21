using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICourseService
    {
        Task<List<Course>> GetAllAsync();
        Task<Course> GetByIdAsync(int id);

        Task CreateAsync(Course course);
        Task UpdateAsync(Course course);
        Task DeleteAsync(int id);

        //for notification
        Task CreateAsync(Course course, int adminUserId);
        //for notification
        Task UpdateAsync(Course course, int adminUserId);
        //for notification
        Task DeleteAsync(int id, int adminUserId);

        Task<bool> HasDependenciesAsync(int courseId);
    }
}
