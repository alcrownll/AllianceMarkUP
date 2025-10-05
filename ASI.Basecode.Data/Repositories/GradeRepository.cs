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
    public class GradeRepository : BaseRepository, IGradeRepository
    {
        public GradeRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<Grade> GetGrades() => this.GetDbSet<Grade>();
        
        public IQueryable<Grade> GetGradesByAssignedCourse(int assignedCourseId) => 
            this.GetDbSet<Grade>().Where(g => g.AssignedCourseId == assignedCourseId);
        
        public async Task<bool> BulkUpdateGradesAsync(List<Grade> grades)
        {
            try
            {
                foreach (var grade in grades)
                {
                    this.GetDbSet<Grade>().Update(grade);
                }
                await UnitOfWork.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public Grade GetGradeById(int GradeId) => this.GetDbSet<Grade>().FirstOrDefault(e => e.GradeId == GradeId);

        public void AddGrade(Grade Grade)
        {
            this.GetDbSet<Grade>().Add(Grade);
            UnitOfWork.SaveChanges();
        }

        public void UpdateGrade(Grade Grade)
        {
            this.GetDbSet<Grade>().Update(Grade);
            UnitOfWork.SaveChanges();
        }

        public void DeleteGrade(int GradeId)
        {
            var ec = GetGradeById(GradeId);
            if (ec != null)
            {
                this.GetDbSet<Grade>().Remove(ec);
                UnitOfWork.SaveChanges();
            }
        }
    }
}