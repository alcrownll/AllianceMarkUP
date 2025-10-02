using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;                     // LINQ extensions
using Microsoft.EntityFrameworkCore;   // EF async extensions

namespace ASI.Basecode.Services.Services
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _repo;
        private readonly IUnitOfWork _uow;

        public CourseService(ICourseRepository repo, IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        public async Task<List<Course>> GetAllAsync() =>
            await _repo.GetCourses()
                       .OrderBy(c => c.CourseCode)
                       .ToListAsync(); // use .ToList() if your repo isn't EF-backed

        public async Task CreateAsync(Course course)
        {
            course.TotalUnits = course.LecUnits + course.LabUnits;
            _repo.AddCourse(course);
            await _uow.SaveChangesAsync();
        }

        public async Task UpdateAsync(Course course)
        {
            course.TotalUnits = course.LecUnits + course.LabUnits;
            _repo.UpdateCourse(course);
            await _uow.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            _repo.DeleteCourse(id);
            await _uow.SaveChangesAsync();
        }
    }
}
