using ASI.Basecode.Data.Models;
using System.Linq;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IProgramCourseRepository
    {
        IQueryable<ProgramCourse> GetProgramCourses();
        ProgramCourse GetProgramCourseById(int programCourseId);

        // Common queries for your UI
        IEnumerable<ProgramCourse> GetByProgram(int programId);
        IEnumerable<ProgramCourse> GetByProgramAndYearTerm(int programId, int yearLevel, int term);

        void AddProgramCourse(ProgramCourse programCourse);
        void UpdateProgramCourse(ProgramCourse programCourse);
        void DeleteProgramCourse(int programCourseId);
    }
}
