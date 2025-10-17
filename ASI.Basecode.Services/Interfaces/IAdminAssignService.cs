using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminAssignService
    {
        Task<IReadOnlyList<AssignedCourse>> GetListAsync(string q = null);
        Task<IReadOnlyList<Course>> GetCoursesAsync();
        Task<IReadOnlyList<Program>> GetProgramsAsync();
        Task<IReadOnlyList<Teacher>> GetTeachersWithUsersAsync();

        Task<PagedResultModel<Student>> GetStudentsForAssignAsync(
            string program,
            string yearLevel,
            string section,
            string status,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<string> GenerateEdpCodeAsync(
            int courseId,
            string semester,
            string schoolYear,
            CancellationToken ct = default);

        Task<int> CreateAssignedCourseAsync(
            AssignedCourse form,
            string blockProgram,
            string blockYear,
            string blockSection,
            IEnumerable<int> extraStudentIds,
            CancellationToken ct = default);

        Task<AssignedCourse> GetAssignedCourseAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<Student>> GetEnrolledStudentsAsync(int assignedCourseId, CancellationToken ct);

        Task<IReadOnlyList<ClassSchedule>> GetSchedulesAsync(int assignedCourseId, CancellationToken ct);

        Task CreateSchedulesAsync(
            int assignedCourseId,
            string room,
            string startTimeHHmm,
            string endTimeHHmm,
            IEnumerable<int> days,
            CancellationToken ct);

        Task UpsertSchedulesAsync(
            int assignedCourseId,
            string room,
            string startTimeHHmm,
            string endTimeHHmm,
            IEnumerable<int> days,
            CancellationToken ct);

        Task<(IReadOnlyList<Student> Items, int Total)> GetAddableStudentsPageAsync(
            int assignedCourseId,
            string blockProgram,
            string blockYear,
            string blockSection,
            string status,
            int page,
            int pageSize,
            CancellationToken ct);

        Task UpdateAssignedCourseAsync(
            AssignedCourse posted,
            IEnumerable<int> removeStudentIds,
            IEnumerable<int> addStudentIds,
            CancellationToken ct);

        Task<(bool ok, string message)> DeleteAssignedCourseAsync(int assignedCourseId, CancellationToken ct);
    }
}
