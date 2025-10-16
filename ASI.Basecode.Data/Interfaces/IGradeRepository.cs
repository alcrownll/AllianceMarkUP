using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IGradeRepository
    {
        IQueryable<Grade> GetGrades();
        IQueryable<Grade> GetGradesByAssignedCourse(int assignedCourseId);
        Task<bool> BulkUpdateGradesAsync(List<Grade> grades);
        Grade GetGradeById(int GradeId);
        void AddGrade(Grade Grade);
        void UpdateGrade(Grade Grade);
        void DeleteGrade(int GradeId);
        void AddGradesNoSave(IEnumerable<Grade> grades);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}