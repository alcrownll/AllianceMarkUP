using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICurriculumService
    {
        Program CreateProgram(string code, string name, string notes);
        Program ActivateProgram(int programId); // optional
        ProgramCourse AddCourseToTerm(int programId, int year, int term, int courseId, int prereqCourseId);
        IEnumerable<ProgramCourse> GetTerm(int programId, int year, int term);
        void RemoveProgramCourse(int programCourseId); // optional

        bool HasAnyCourses(int programId);
        void DiscardProgram(int programId);

        Program CreateProgramWithCurriculum(ComposeProgramDto dto);
    }
}
