using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IStudentRepository
    {
        IQueryable<Student> GetStudents();
        IQueryable<Student> GetStudentsWithUser();
        Student GetStudentById(int studentId);
        void AddStudent(Student student);
        void UpdateStudent(Student student);
        void DeleteStudent(int studentId);
    }
}
