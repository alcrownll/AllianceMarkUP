using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _repo;
        private readonly IUnitOfWork _uow;

        // Primary constructor
        public CourseService(ICourseRepository repo, IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        // Get all courses, ordered by course code
        public async Task<List<Course>> GetAllAsync() =>
            await _repo.GetCourses()
                       .OrderBy(c => c.CourseCode)
                       .ToListAsync();

        // Get a course by its ID
        public async Task<Course?> GetByIdAsync(int id) =>
            await _repo.GetCourses()
                       .AsNoTracking() // Prevent tracking for read-only operations
                       .FirstOrDefaultAsync(c => c.CourseId == id);

        // Create a new course
        public async Task CreateAsync(Course course)
        {
            course.TotalUnits = course.LecUnits + course.LabUnits;
            _repo.AddCourse(course);
            await _uow.SaveChangesAsync();
        }

        // Update an existing course
        public async Task UpdateAsync(Course course)
        {
            // Calculate total units
            course.TotalUnits = course.LecUnits + course.LabUnits;

            // Detach any existing tracked entity with the same key
            var trackedEntity = _uow.Database.ChangeTracker.Entries<Course>()
                .FirstOrDefault(e => e.Entity.CourseId == course.CourseId);

            if (trackedEntity != null)
            {
                trackedEntity.State = EntityState.Detached;
            }

            // Now update the course
            _repo.UpdateCourse(course);
            await _uow.SaveChangesAsync();
        }

        // Delete a course after checking for dependencies
        public async Task DeleteAsync(int id)
        {
            // Checking if the course has dependencies before deletion
            if (await HasDependenciesAsync(id))
            {
                // If dependencies exist, throw an exception or handle accordingly
                throw new InvalidOperationException("This course cannot be deleted because it is referenced in ProgramCourses or AssignedCourses.");
            }
            _repo.DeleteCourse(id);
            await _uow.SaveChangesAsync();
        }

        // New method to check for dependencies before deletion
        public async Task<bool> HasDependenciesAsync(int courseId)
        {
            // Check if the course is referenced in ProgramCourses or AssignedCourses
            bool isReferencedInProgramCourses = await _uow.Database.Set<ProgramCourse>()
                .AnyAsync(pc => pc.CourseId == courseId);

            bool isReferencedInAssignedCourses = await _uow.Database.Set<AssignedCourse>()
                .AnyAsync(ac => ac.CourseId == courseId);

            return isReferencedInProgramCourses || isReferencedInAssignedCourses;
        }
    }
}