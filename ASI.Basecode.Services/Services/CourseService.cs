using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Exceptions;
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
            // Check for duplicate course code
            var existingCourse = await _repo.GetCourses()
                .FirstOrDefaultAsync(c => c.CourseCode.ToLower() == course.CourseCode.ToLower());

            if (existingCourse != null)
            {
                throw new DuplicateCourseCodeException(course.CourseCode);
            }

            course.TotalUnits = course.LecUnits + course.LabUnits;
            _repo.AddCourse(course);
            await _uow.SaveChangesAsync();
        }

        // Update an existing course
        public async Task UpdateAsync(Course course)
        {
            // Check if course exists
            var existingCourse = await GetByIdAsync(course.CourseId);
            if (existingCourse == null)
            {
                throw new NotFoundException("Course", course.CourseId);
            }

            // Check for duplicate course code (excluding the current course)
            var duplicateCourse = await _repo.GetCourses()
                .FirstOrDefaultAsync(c =>
                    c.CourseCode.ToLower() == course.CourseCode.ToLower() &&
                    c.CourseId != course.CourseId);

            if (duplicateCourse != null)
            {
                throw new DuplicateCourseCodeException(course.CourseCode);
            }

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
            // Check if course exists
            var course = await GetByIdAsync(id);
            if (course == null)
            {
                throw new NotFoundException("Course", id);
            }

            // Check if course is referenced in ProgramCourses
            var programCodes = await _uow.Database.Set<ProgramCourse>()
                .Where(pc => pc.CourseId == id)
                .Join(
                    _uow.Database.Set<Program>(),
                    pc => pc.ProgramId,
                    p => p.ProgramId,
                    (pc, p) => p.ProgramCode
                )
                .ToListAsync();

            if (programCodes.Any())
            {
                throw new CourseInUseException(course.CourseCode, programCodes);
            }
           
            // Safe to delete
            _repo.DeleteCourse(id);
            await _uow.SaveChangesAsync();
        }

        // Checks if Course is referenced in a ProgramCourse or AssignedCourse
        public async Task<bool> HasDependenciesAsync(int courseId)
        {
            bool isReferencedInProgramCourses = await _uow.Database.Set<ProgramCourse>()
                .AnyAsync(pc => pc.CourseId == courseId);

            bool isReferencedInAssignedCourses = await _uow.Database.Set<AssignedCourse>()
                .AnyAsync(ac => ac.CourseId == courseId);

            return isReferencedInProgramCourses || isReferencedInAssignedCourses;
        }
    }
}
