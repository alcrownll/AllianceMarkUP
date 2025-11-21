using ASI.Basecode.Data.Models;
using System.Linq;


namespace ASI.Basecode.Data.Interfaces
{
    public interface ICourseRepository
    {
        IQueryable<Course> GetCourses();
        Course GetCourseById(int courseId);
        void AddCourse(Course course);
        void UpdateCourse(Course course);
        void DeleteCourse(int courseId);
    }
}