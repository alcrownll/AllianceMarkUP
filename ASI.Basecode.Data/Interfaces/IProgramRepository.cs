using ASI.Basecode.Data.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IProgramRepository
    {
        IQueryable<Program> GetPrograms();
        Program GetProgramById(int programId);
        Program GetProgramByCode(string programCode);

        void AddProgram(Program program);
        void UpdateProgram(Program program);
        void DeleteProgram(int programId);
        Task<bool> SetActiveAsync(int programId, bool isActive);
    }
}
