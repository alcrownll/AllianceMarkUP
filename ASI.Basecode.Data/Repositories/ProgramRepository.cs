using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Repositories
{
    public class ProgramRepository : IProgramRepository
    {
        private readonly AsiBasecodeDBContext _ctx;
        public ProgramRepository(AsiBasecodeDBContext ctx) => _ctx = ctx;

        public IQueryable<Program> GetPrograms() => _ctx.Programs;

        public Program GetProgramById(int programId) =>
            _ctx.Programs.FirstOrDefault(p => p.ProgramId == programId);

        public Program GetProgramByCode(string programCode) =>
            _ctx.Programs.FirstOrDefault(p => p.ProgramCode == programCode);

        public void AddProgram(Program program)
        {
            _ctx.Programs.Add(program);
            _ctx.SaveChanges();
        }

        public void UpdateProgram(Program program)
        {
            _ctx.Programs.Update(program);
            _ctx.SaveChanges();
        }

        public void DeleteProgram(int programId)
        {
            var entity = GetProgramById(programId);
            if (entity == null) return;
            _ctx.Programs.Remove(entity);
            _ctx.SaveChanges();
        }
    }
}
