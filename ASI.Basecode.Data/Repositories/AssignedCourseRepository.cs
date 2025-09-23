using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class AssignedCourseRepository : BaseRepository, IAssignedCourseRepository
    {
        public AssignedCourseRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<AssignedCourse> GetAssignedCourses() => this.GetDbSet<AssignedCourse>();
        public AssignedCourse GetAssignedCourseById(int assignedCourseId) => this.GetDbSet<AssignedCourse>().FirstOrDefault(a => a.AssignedCourseId == assignedCourseId);

        public void AddAssignedCourse(AssignedCourse assignedCourse)
        {
            this.GetDbSet<AssignedCourse>().Add(assignedCourse);
            UnitOfWork.SaveChanges();
        }

        public void UpdateAssignedCourse(AssignedCourse assignedCourse)
        {
            this.GetDbSet<AssignedCourse>().Update(assignedCourse);
            UnitOfWork.SaveChanges();
        }

        public void DeleteAssignedCourse(int assignedCourseId)
        {
            var ac = GetAssignedCourseById(assignedCourseId);
            if (ac != null)
            {
                this.GetDbSet<AssignedCourse>().Remove(ac);
                UnitOfWork.SaveChanges();
            }
        }
    }
}