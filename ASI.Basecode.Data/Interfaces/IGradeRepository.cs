using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IGradeRepository
    {
        IQueryable<Grade> GetGrades();
        Grade GetGradeById(int GradeId);
        void AddGrade(Grade Grade);
        void UpdateGrade(Grade Grade);
        void DeleteGrade(int GradeId);
    }
}