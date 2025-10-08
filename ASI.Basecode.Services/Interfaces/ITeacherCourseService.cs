using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ITeacherCourseService
    {
        // For Grade View tab - Getting assigned courses and students
        Task<List<TeacherCourseViewModel>> GetTeacherAssignedCoursesAsync(int teacherId, string semester = null);
        Task<List<StudentGradeViewModel>> GetStudentsInCourseAsync(int assignedCourseId);
        
        // For Grade Entry tab - Managing class schedules and grades
        Task<List<TeacherClassScheduleViewModel>> GetTeacherClassSchedulesAsync(int teacherId, string semester = null);
        Task<List<StudentGradeViewModel>> GetStudentGradesForClassAsync(int assignedCourseId, string examType = null);
        Task<bool> UpdateStudentGradesAsync(List<StudentGradeUpdateModel> grades);
        
        // For filtering functionality
        Task<List<string>> GetTeacherProgramsAsync(int teacherId);
        Task<List<int>> GetTeacherYearLevelsAsync(int teacherId);
        Task<List<StudentGradeViewModel>> SearchStudentsAsync(int teacherId, string searchName = null, string searchId = null, string program = null, int? yearLevel = null);
        

    }
}