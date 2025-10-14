using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;

namespace ASI.Basecode.Services.Services
{
    public class CurriculumService : ICurriculumService
    {
        private readonly IProgramRepository _programs;
        private readonly IProgramCourseRepository _progCourses;
        private readonly IYearTermRepository _yearTerms;

        public CurriculumService(IProgramRepository programs,
                                 IProgramCourseRepository progCourses,
                                 IYearTermRepository yearTerms)
        {
            _programs = programs;
            _progCourses = progCourses;
            _yearTerms = yearTerms;
        }

        public Program CreateProgram(string code, string name, string notes)
        {
            var entity = new Program
            {
                ProgramCode = (code ?? "").Trim(),
                ProgramName = (name ?? "").Trim(),
                IsActive = false
            };
            _programs.AddProgram(entity);
            return entity;
        }

        public Program ActivateProgram(int programId)
        {
            var p = _programs.GetProgramById(programId);
            if (p == null) return null;
            p.IsActive = true;
            _programs.UpdateProgram(p);
            return p;
        }

        public ProgramCourse AddCourseToTerm(int programId, int year, int term, int courseId, int prereqCourseId)
        {
            var yt = _yearTerms.GetYearTerm(year, term);
            var pc = new ProgramCourse
            {
                ProgramId = programId,
                CourseId = courseId,
                YearTermId = yt.YearTermId,
                Prerequisite = prereqCourseId
            };
            _progCourses.AddProgramCourse(pc);
            return pc;
        }

        public IEnumerable<ProgramCourse> GetTerm(int programId, int year, int term)
        {
            return _progCourses.GetByProgramAndYearTerm(programId, year, term);
        }

        public void RemoveProgramCourse(int programCourseId)
        {
            _progCourses.DeleteProgramCourse(programCourseId);
        }

        public bool HasAnyCourses(int programId)
        {
            // if you have a repo call for this, use it; otherwise quick LINQ:
            return _progCourses.GetByProgram(programId).Any();
        }

        public void DiscardProgram(int programId)
        {
            // FK Program → ProgramCourses is Cascade (per our model config), so this deletes children too
            _programs.DeleteProgram(programId);
        }


        public Program CreateProgramWithCurriculum(ComposeProgramDto dto)
        {
            // Ambient transaction so all repo/service calls commit together
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var program = CreateProgram(dto.Code.Trim(), dto.Name.Trim(), dto.Notes);

            foreach (var y in dto.Years ?? Enumerable.Empty<ComposeYearDto>())
            {
                foreach (var t in y.Terms ?? Enumerable.Empty<ComposeTermDto>())
                {
                    foreach (var c in t.Courses ?? Enumerable.Empty<ComposeCourseDto>())
                    {
                        AddCourseToTerm(program.ProgramId, y.Year, t.Term, c.CourseId, c.PrereqCourseId);
                    }
                }
            }

            scope.Complete();
            return program;
        }


    }
}
