// ASI.Basecode.Services/Interfaces/ICourseService.cs
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

        // Add the method to check dependencies before deletion
        Task<bool> HasDependenciesAsync(int courseId);  // New method
    }
}
