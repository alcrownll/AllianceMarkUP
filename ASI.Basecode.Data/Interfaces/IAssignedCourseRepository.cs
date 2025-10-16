using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAssignedCourseRepository
    {
        IQueryable<AssignedCourse> GetAssignedCourses();
        IQueryable<AssignedCourse> GetAssignedCoursesByTeacher(int teacherId);
        IQueryable<AssignedCourse> GetAssignedCoursesByTeacherAndSemester(int teacherId, string semester);
        AssignedCourse GetAssignedCourseById(int assignedCourseId);
        void AddAssignedCourse(AssignedCourse assignedCourse);
        void UpdateAssignedCourse(AssignedCourse assignedCourse);
        void DeleteAssignedCourse(int assignedCourseId);

        void AddAssignedCourseNoSave(AssignedCourse assignedCourse);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}