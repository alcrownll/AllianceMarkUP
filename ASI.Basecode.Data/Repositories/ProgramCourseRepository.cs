using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

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
            var entity = _ctx.ProgramCourses.Find(programCourseId);
            if (entity == null) return;
            _ctx.ProgramCourses.Remove(entity);
            _ctx.SaveChanges();
        }

    }
}
