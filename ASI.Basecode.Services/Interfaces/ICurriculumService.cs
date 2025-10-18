using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICurriculumService
    {
        // Programs
        Program CreateProgram(string code, string name, string notes);
        IEnumerable<Program> ListPrograms(string q = null);
        Program ActivateProgram(int programId);         // optional if you use IsActive toggle elsewhere
        void DiscardProgram(int programId);
        bool HasAnyCourses(int programId);

        // ProgramCourses
        ProgramCourse AddCourseToTerm(int programId, int year, int term, int courseId, int prereqCourseId);
        IEnumerable<ProgramCourse> GetTerm(int programId, int year, int term);
        void RemoveProgramCourse(int programCourseId);
        bool ProgramOwnsProgramCourse(int programId, int programCourseId);
        bool TryRemoveProgramCourse(int programId, int programCourseId);
        Task<bool> SetProgramActiveAsync(int id, bool isActive);

        // Services/Interfaces/ICurriculumService.cs
        void AddCoursesToTermBulk(int programId, int year, int term, int[] courseIds, int[] prereqIds);

    }
}
