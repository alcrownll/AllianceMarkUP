using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore; 
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class StudentRepository : BaseRepository, IStudentRepository
    {
        public StudentRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<Student> GetStudents() => this.GetDbSet<Student>();

        public IQueryable<Student> GetStudentsWithUser() =>
            this.GetDbSet<Student>()
                .Include(s => s.User)
                .AsNoTracking();

        public Student GetStudentById(int studentId) =>
            this.GetDbSet<Student>().FirstOrDefault(x => x.StudentId == studentId);

        public void AddStudent(Student student)
        {
            this.GetDbSet<Student>().Add(student);
            UnitOfWork.SaveChanges();
        }

        public void UpdateStudent(Student student)
        {
            this.GetDbSet<Student>().Update(student);
            UnitOfWork.SaveChanges();
        }

        public void DeleteStudent(int studentId)
        {
            var student = GetStudentById(studentId);
            if (student != null)
            {
                this.GetDbSet<Student>().Remove(student);
                UnitOfWork.SaveChanges();
            }
        }
    }
}
