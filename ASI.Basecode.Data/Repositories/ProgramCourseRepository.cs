using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class ProgramCourseRepository : IProgramCourseRepository
    {
        private readonly AsiBasecodeDBContext _ctx;
        public ProgramCourseRepository(AsiBasecodeDBContext ctx) => _ctx = ctx;

        public IQueryable<ProgramCourse> GetProgramCourses() =>
            _ctx.ProgramCourses;

        public ProgramCourse GetProgramCourseById(int programCourseId) =>
            _ctx.ProgramCourses
                .Include(pc => pc.Course)
                .Include(pc => pc.YearTerm)
                .FirstOrDefault(pc => pc.ProgramCourseId == programCourseId);

        public IEnumerable<ProgramCourse> GetByProgram(int programId) =>
            _ctx.ProgramCourses
                .Include(pc => pc.Course)
                .Include(pc => pc.YearTerm)
                .Where(pc => pc.ProgramId == programId)
                .ToList();

        public IEnumerable<ProgramCourse> GetByProgramAndYearTerm(int programId, int yearLevel, int term) =>
            _ctx.ProgramCourses
                .Include(pc => pc.Course)
                .Include(pc => pc.YearTerm)
                .Where(pc => pc.ProgramId == programId &&
                             pc.YearTerm.YearLevel == yearLevel &&
                             pc.YearTerm.TermNumber == term)
                .ToList();

        public void AddProgramCourse(ProgramCourse programCourse)
        {
            _ctx.ProgramCourses.Add(programCourse);
            _ctx.SaveChanges();
        }

        public void UpdateProgramCourse(ProgramCourse programCourse)
        {
            _ctx.ProgramCourses.Update(programCourse);
            _ctx.SaveChanges();
        }

        public void DeleteProgramCourse(int programCourseId)
        {
            // Attach a stub to avoid a read, then delete.
            var stub = new ProgramCourse { ProgramCourseId = programCourseId };
            _ctx.Entry(stub).State = EntityState.Deleted;

            try
            {
                _ctx.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
              
            }
        }
    }
}
