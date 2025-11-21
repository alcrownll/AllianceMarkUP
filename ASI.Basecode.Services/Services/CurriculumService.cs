using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
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
        private readonly INotificationService _notif;

        // ✅ CHANGE THIS IF YOU WANT A DIFFERENT CAP
        private const int MAX_UNITS_PER_TERM = 24;

        public CurriculumService(
            IProgramRepository programs,
            IProgramCourseRepository progCourses,
            IYearTermRepository yearTerms,
            INotificationService notif)
        {
            _programs = programs;
            _progCourses = progCourses;
            _yearTerms = yearTerms;
            _notif = notif;
        }

        public Program CreateProgram(string code, string name, string notes)
            => CreateProgram(code, name, notes, adminUserId: 0);

        public Program CreateProgram(string code, string name, string notes, int adminUserId)
        {
            var codeNorm = (code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(codeNorm))
                throw new InvalidOperationException("Program code is required.");

            var nameNorm = (name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nameNorm))
                throw new InvalidOperationException("Program name is required.");

            // ✅ DUPLICATE CHECK: Program Code
            var dupCode = _programs.GetPrograms()
                .AsNoTracking()
                .Any(p => p.ProgramCode != null &&
                          p.ProgramCode.Trim().ToLower() == codeNorm.ToLower());
            if (dupCode)
                throw new DuplicateProgramException("code", codeNorm);

            // ✅ DUPLICATE CHECK: Program Name
            var dupName = _programs.GetPrograms()
                .AsNoTracking()
                .Any(p => p.ProgramName != null &&
                          p.ProgramName.Trim().ToLower() == nameNorm.ToLower());
            if (dupName)
                throw new DuplicateProgramException("name", nameNorm);

            var entity = new Program
            {
                ProgramCode = codeNorm,
                ProgramName = nameNorm,
                IsActive = false
            };

            _programs.AddProgram(entity);

            if (adminUserId > 0)
            {
                _notif.NotifyAdminCreatedProgram(
                    adminUserId: adminUserId,
                    programCode: entity.ProgramCode,
                    programName: entity.ProgramName
                );
            }

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

        public bool UpdateProgram(int id, string code, string name, bool isActive)
            => UpdateProgram(id, code, name, isActive, adminUserId: 0);

        public bool UpdateProgram(int id, string code, string name, bool isActive, int adminUserId)
        {
            var entity = _programs.GetProgramById(id);
            if (entity == null) return false;

            var codeNorm = (code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(codeNorm))
                throw new InvalidOperationException("Program code is required.");

            var nameNorm = (name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nameNorm))
                throw new InvalidOperationException("Program name is required.");

            // ✅ DUPLICATE CHECK: Program Code (excluding current)
            var dupCode = _programs.GetPrograms()
                .AsNoTracking()
                .Any(p => p.ProgramId != id &&
                          p.ProgramCode != null &&
                          p.ProgramCode.Trim().ToLower() == codeNorm.ToLower());
            if (dupCode)
                throw new DuplicateProgramException("code", codeNorm);

            // ✅ DUPLICATE CHECK: Program Name (excluding current)
            var dupName = _programs.GetPrograms()
                .AsNoTracking()
                .Any(p => p.ProgramId != id &&
                          p.ProgramName != null &&
                          p.ProgramName.Trim().ToLower() == nameNorm.ToLower());
            if (dupName)
                throw new DuplicateProgramException("name", nameNorm);

            entity.ProgramCode = codeNorm;
            entity.ProgramName = nameNorm;
            entity.IsActive = isActive;

            _programs.UpdateProgram(entity);

            if (adminUserId > 0)
            {
                _notif.NotifyAdminUpdatedProgram(
                    adminUserId: adminUserId,
                    programCode: entity.ProgramCode,
                    programName: entity.ProgramName,
                    isActive: isActive
                );
            }

            return true;
        }

        public void DiscardProgram(int programId)
            => DiscardProgram(programId, adminUserId: 0);

        public void DiscardProgram(int programId, int adminUserId)
        {
            var p = _programs.GetProgramById(programId);
            if (p == null) return;

            var hadCourses = HasAnyCourses(programId);

            _programs.DeleteProgram(programId);

            if (adminUserId > 0)
            {
                _notif.NotifyAdminDeletedProgram(
                    adminUserId: adminUserId,
                    programCode: p.ProgramCode,
                    programName: p.ProgramName,
                    forceDelete: hadCourses
                );
            }
        }

        public bool HasAnyCourses(int programId)
        {
            return _progCourses.GetByProgram(programId).Any();
        }

        public IEnumerable<Program> ListPrograms(string q = null)
        {
            IQueryable<Program> query = _programs.GetPrograms().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                var pattern = $"%{q}%";

                query = query.Where(p =>
                    (p.ProgramCode != null && EF.Functions.ILike(p.ProgramCode, pattern)) ||
                    (p.ProgramName != null && EF.Functions.ILike(p.ProgramName, pattern)));
            }

            return query
                .OrderBy(p => p.ProgramCode)
                .Take(500)
                .ToList();
        }

        public Task<bool> SetProgramActiveAsync(int id, bool isActive)
           => _programs.SetActiveAsync(id, isActive);

        // =====================================================
        // PROGRAM COURSE METHODS
        // =====================================================

        // ✅ Helper: get current total units for a term
        private int GetCurrentTermUnits(int programId, int year, int term)
        {
            var existingTermCourses = _progCourses
                .GetByProgramAndYearTerm(programId, year, term)
                .Where(pc => pc.Course != null);

            return existingTermCourses.Sum(pc => (pc.Course.LecUnits + pc.Course.LabUnits));
        }

        // ✅ Helper: get candidate course units by courseId
        // We try to find the course via any ProgramCourse that references it.
        // If not found, we block to avoid letting invalid adds bypass the cap.
        private int GetCourseUnitsOrThrow(int courseId)
        {
            var anyPcWithCourse = _progCourses.GetProgramCourses()
                .AsNoTracking()
                .Include(pc => pc.Course)
                .FirstOrDefault(pc => pc.CourseId == courseId);

            var course = anyPcWithCourse?.Course;
            if (course == null)
                throw new InvalidOperationException($"Course not found (CourseId={courseId}) for unit check.");

            return course.LecUnits + course.LabUnits;
        }

        public ProgramCourse AddCourseToTerm(int programId, int year, int term, int courseId, int prereqCourseId)
        {
            var currentUnits = GetCurrentTermUnits(programId, year, term);
            var newCourseUnits = GetCourseUnitsOrThrow(courseId);

            if (currentUnits + newCourseUnits > MAX_UNITS_PER_TERM)
            {
                throw new InvalidOperationException(
                    $"Cannot add course. This semester already has {currentUnits} unit(s). " +
                    $"Adding this course ({newCourseUnits} unit(s)) exceeds the maximum of {MAX_UNITS_PER_TERM} unit(s) per semester."
                );
            }

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

        public bool ProgramOwnsProgramCourse(int programId, int programCourseId)
        {
            return _progCourses
                .GetProgramCourses()
                .Any(pc => pc.ProgramCourseId == programCourseId &&
                           pc.ProgramId == programId);
        }

        public bool TryRemoveProgramCourse(int programId, int programCourseId)
        {
            var belongs = ProgramOwnsProgramCourse(programId, programCourseId);
            if (!belongs) return false;

            _progCourses.DeleteProgramCourse(programCourseId);
            return true;
        }

        public void AddCoursesToTermBulk(int programId, int year, int term, int[] courseIds, int[] prereqIds)
        {
            if (courseIds == null || courseIds.Length == 0) return;

            var yt = _yearTerms.GetYearTerm(year, term)
                     ?? throw new InvalidOperationException($"YearTerm not found (Y{year} T{term}). Seed the lookup table.");

            var existing = _progCourses.GetByProgramAndYearTerm(programId, year, term)
                                       .Select(x => x.CourseId)
                                       .ToHashSet();

            // ✅ Pre-validate cap BEFORE adding anything
            var currentUnits = GetCurrentTermUnits(programId, year, term);

            // unique + valid new ids
            var toAdd = new List<int>();
            for (int i = 0; i < courseIds.Length; i++)
            {
                var cid = courseIds[i];
                if (cid <= 0 || existing.Contains(cid)) continue;
                if (!toAdd.Contains(cid)) toAdd.Add(cid);
            }

            // simulate addition to check max
            int simulatedTotal = currentUnits;
            foreach (var cid in toAdd)
            {
                var units = GetCourseUnitsOrThrow(cid);
                if (simulatedTotal + units > MAX_UNITS_PER_TERM)
                {
                    throw new InvalidOperationException(
                        $"Cannot add selected courses. This semester already has {currentUnits} unit(s). " +
                        $"Adding the selected list would exceed the maximum of {MAX_UNITS_PER_TERM} unit(s) per semester."
                    );
                }
                simulatedTotal += units;
            }

            // ✅ Safe to add
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
    }
}
