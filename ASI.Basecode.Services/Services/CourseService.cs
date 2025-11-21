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
        private readonly INotificationService _notif; //for notification

        public CourseService(ICourseRepository repo, IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
            _notif = notif; //for notification
        }

        public async Task<List<Course>> GetAllAsync() =>
            await _repo.GetCourses()
                       .OrderBy(c => c.CourseCode)
                       .ToListAsync();

        public async Task<Course?> GetByIdAsync(int id) =>
            await _repo.GetCourses()
                       .AsNoTracking() 
                       .FirstOrDefaultAsync(c => c.CourseId == id);

       
        public async Task CreateAsync(Course course)
        {
            
            var existingCourse = await _repo.GetCourses()
                .FirstOrDefaultAsync(c => c.CourseCode.ToLower() == course.CourseCode.ToLower());

            if (existingCourse != null)
            {
                throw new DuplicateCourseCodeException(course.CourseCode);
            }

            course.TotalUnits = course.LecUnits + course.LabUnits;
            _repo.AddCourse(course);
            await _uow.SaveChangesAsync();

            if (adminUserId > 0) //for notification
            {
                _notif.NotifyAdminCreatedCourse(
                    adminUserId,
                    course.CourseCode,
                    course.Description
                );
            }
        }

     
        public async Task UpdateAsync(Course course)
        {
            
            var existingCourse = await GetByIdAsync(course.CourseId);
            if (existingCourse == null)
            {
                throw new NotFoundException("Course", course.CourseId);
            }

            var duplicateCourse = await _repo.GetCourses()
                .FirstOrDefaultAsync(c =>
                    c.CourseCode.ToLower() == course.CourseCode.ToLower() &&
                    c.CourseId != course.CourseId);

            if (duplicateCourse != null)
            {
                throw new DuplicateCourseCodeException(course.CourseCode);
            }

            course.TotalUnits = course.LecUnits + course.LabUnits;

      
            var trackedEntity = _uow.Database.ChangeTracker.Entries<Course>()
                .FirstOrDefault(e => e.Entity.CourseId == course.CourseId);

            if (trackedEntity != null)
            {
                trackedEntity.State = EntityState.Detached;
            }

           
            _repo.UpdateCourse(course);
            await _uow.SaveChangesAsync();

            if (adminUserId > 0) //for notification
            {
                _notif.NotifyAdminUpdatedCourse(
                    adminUserId,
                    course.CourseCode,
                    course.Description
                );
            }
        }

       
        public async Task DeleteAsync(int id)
        {
            
            var course = await GetByIdAsync(id);
            if (course == null)
            {
                throw new NotFoundException("Course", id);
            }

            
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
           
          
            _repo.DeleteCourse(id);
            await _uow.SaveChangesAsync();

            if (adminUserId > 0) //for notification
            {
                _notif.NotifyAdminDeletedCourse(
                    adminUserId,
                    course.CourseCode,
                    course.Description
                );
            }
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
