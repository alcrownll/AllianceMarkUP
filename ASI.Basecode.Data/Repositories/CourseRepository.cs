using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System.Linq;


namespace ASI.Basecode.Data.Repositories
{
    public class CourseRepository : BaseRepository, ICourseRepository
    {
        public CourseRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<Course> GetCourses() => this.GetDbSet<Course>();
        public Course GetCourseById(int courseId) => this.GetDbSet<Course>().FirstOrDefault(c => c.CourseId == courseId);

        public void AddCourse(Course course)
        {
            this.GetDbSet<Course>().Add(course);
            UnitOfWork.SaveChanges();
        }

        public void UpdateCourse(Course course)
        {
            this.GetDbSet<Course>().Update(course);
            UnitOfWork.SaveChanges();
        }

        public void DeleteCourse(int courseId)
        {
            var course = GetCourseById(courseId);
            if (course != null)
            {
                this.GetDbSet<Course>().Remove(course);
                UnitOfWork.SaveChanges();
            }
        }
    }
}