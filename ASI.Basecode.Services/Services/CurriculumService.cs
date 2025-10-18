using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Task<bool> SetProgramActiveAsync(int id, bool isActive)
       => _programs.SetActiveAsync(id, isActive);

        public IEnumerable<ProgramCourse> GetTerm(int programId, int year, int term)
        {
            return _progCourses.GetByProgramAndYearTerm(programId, year, term);
        }

        public void RemoveProgramCourse(int programCourseId)
        {
            // Legacy, ID-only delete
            _progCourses.DeleteProgramCourse(programCourseId);
        }

        // ---------- NEW: program-scoped safety helpers ----------
        // Add both signatures to ICurriculumService

        /// <summary>
        /// Returns true if the ProgramCourse belongs to the Program.
        /// </summary>
        public bool ProgramOwnsProgramCourse(int programId, int programCourseId)
        {
            return _progCourses
                .GetProgramCourses()
                .Any(pc => pc.ProgramCourseId == programCourseId &&
                           pc.ProgramId == programId);
        }

        /// <summary>
        /// Removes the ProgramCourse only if it belongs to the given Program.
        /// Returns true when removed, false if not found / not owned.
        /// </summary>
        public bool TryRemoveProgramCourse(int programId, int programCourseId)
        {
            // Ownership check keeps deletes scoped to the current program
            var belongs = ProgramOwnsProgramCourse(programId, programCourseId);
            if (!belongs) return false;

            _progCourses.DeleteProgramCourse(programCourseId);
            return true;
        }
        // --------------------------------------------------------

        public bool HasAnyCourses(int programId)
        {
            return _progCourses.GetByProgram(programId).Any();
        }

        public void DiscardProgram(int programId)
        {
            // Assumes FK Program → ProgramCourses is Cascade
            _programs.DeleteProgram(programId);
        }

        public Program CreateProgramWithCurriculum(ComposeProgramDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Code)) throw new ArgumentException("Program code is required.");
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Program name is required.");

            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var program = CreateProgram(dto.Code.Trim(), dto.Name.Trim(), dto.Notes);

            foreach (var y in dto.Years ?? Enumerable.Empty<ComposeYearDto>())
            {
                foreach (var t in y.Terms ?? Enumerable.Empty<ComposeTermDto>())
                {
                    if (t.Term is < 1 or > 2) throw new ArgumentException($"Invalid term number {t.Term} for year {y.Year}.");

                    foreach (var c in t.Courses ?? Enumerable.Empty<ComposeCourseDto>())
                    {
                        var yt = _yearTerms.GetYearTerm(y.Year, t.Term)
                                 ?? throw new InvalidOperationException($"YearTerm not found for Year={y.Year}, Term={t.Term}. Seed your lookup table.");

                        AddCourseToTerm(program.ProgramId, y.Year, t.Term, c.CourseId, c.PrereqCourseId);
                    }
                }
            }

            scope.Complete();
            return program;
        }

        public IEnumerable<Program> ListPrograms(string q = null)
        {
            var all = _programs.GetPrograms();
            if (string.IsNullOrWhiteSpace(q)) return all;

            q = q.Trim();
            return all.Where(p =>
                (!string.IsNullOrEmpty(p.ProgramCode) && p.ProgramCode.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.ProgramName) && p.ProgramName.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        
        public void AddCoursesToTermBulk(int programId, int year, int term, int[] courseIds, int[] prereqIds)
        {
            if (courseIds == null || courseIds.Length == 0) return;

            var yt = _yearTerms.GetYearTerm(year, term)
                     ?? throw new InvalidOperationException($"YearTerm not found (Y{year} T{term}). Seed the lookup table.");

            var existing = _progCourses.GetByProgramAndYearTerm(programId, year, term)
                                       .Select(x => x.CourseId)
                                       .ToHashSet();

            for (int i = 0; i < courseIds.Length; i++)
            {
                var cid = courseIds[i];
                if (cid <= 0 || existing.Contains(cid)) continue;

                int? prereq = null;
                if (prereqIds != null && i < prereqIds.Length && prereqIds[i] > 0)
                    prereq = prereqIds[i];

                _progCourses.AddProgramCourse(new ProgramCourse
                {
                    ProgramId = programId,
                    CourseId = cid,
                    YearTermId = yt.YearTermId,
                    Prerequisite = prereq
                });

                existing.Add(cid);
            }
        }
    }
}
