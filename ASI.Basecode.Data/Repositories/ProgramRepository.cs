using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<bool> SetActiveAsync(int programId, bool isActive)
        {
            var stub = new Program { ProgramId = programId };

            _ctx.Attach(stub);
            _ctx.Entry(stub).Property(p => p.IsActive).CurrentValue = isActive;
            _ctx.Entry(stub).Property(p => p.IsActive).IsModified = true;

            await _ctx.SaveChangesAsync();
            return true;
        }
    }
}
