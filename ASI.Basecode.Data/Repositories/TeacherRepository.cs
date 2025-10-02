using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore; 
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class TeacherRepository : BaseRepository, ITeacherRepository
    {
        public TeacherRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<Teacher> GetTeachers() => this.GetDbSet<Teacher>();

        public IQueryable<Teacher> GetTeachersWithUser() =>
            this.GetDbSet<Teacher>()
                .Include(t => t.User)
                .AsNoTracking();

        public Teacher GetTeacherById(int teacherId) =>
            this.GetDbSet<Teacher>().FirstOrDefault(x => x.TeacherId == teacherId);

        public void AddTeacher(Teacher teacher)
        {
            this.GetDbSet<Teacher>().Add(teacher);
            UnitOfWork.SaveChanges();
        }

        public void UpdateTeacher(Teacher teacher)
        {
            this.GetDbSet<Teacher>().Update(teacher);
            UnitOfWork.SaveChanges();
        }

        public void DeleteTeacher(int teacherId)
        {
            var teacher = GetTeacherById(teacherId);
            if (teacher != null)
            {
                this.GetDbSet<Teacher>().Remove(teacher);
                UnitOfWork.SaveChanges();
            }
        }
    }
}
