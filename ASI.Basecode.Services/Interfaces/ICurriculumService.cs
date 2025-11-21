using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICurriculumService
    {
        // Programs 
        Program CreateProgram(string code, string name, string notes);
        bool UpdateProgram(int id, string code, string name, bool isActive);
        void DiscardProgram(int programId);

        // For Notification
        Program CreateProgram(string code, string name, string notes, int adminUserId);
        bool UpdateProgram(int id, string code, string name, bool isActive, int adminUserId);
        void DiscardProgram(int programId, int adminUserId);

        IEnumerable<Program> ListPrograms(string q = null);
        Program ActivateProgram(int programId);
        bool HasAnyCourses(int programId);
        Task<bool> SetProgramActiveAsync(int id, bool isActive);

        // ProgramCourses
        ProgramCourse AddCourseToTerm(int programId, int year, int term, int courseId, int prereqCourseId);
        IEnumerable<ProgramCourse> GetTerm(int programId, int year, int term);
        void RemoveProgramCourse(int programCourseId);
        bool ProgramOwnsProgramCourse(int programId, int programCourseId);
        bool TryRemoveProgramCourse(int programId, int programCourseId);
        void AddCoursesToTermBulk(int programId, int year, int term, int[] courseIds, int[] prereqIds);
    }
}
