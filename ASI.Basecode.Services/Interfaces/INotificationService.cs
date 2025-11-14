using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.Interfaces
{
    public interface INotificationService
    {
        // Profile
        void NotifyProfileUpdated(int userId);

        // Admin updated another user's profile
        void NotifyAdminUpdatedUserProfile(
            int adminUserId,
            int targetUserId,
            string targetDisplayName,
            string? targetIdNumber
        );

        // Admin created accounts (single)
        void NotifyAdminCreatedStudent(
            int adminUserId,
            string studentFullName,
            string? idNumber
        );

        void NotifyAdminCreatedTeacher(
            int adminUserId,
            string teacherFullName,
            string? idNumber
        );

        // Admin imported accounts (bulk)
        void NotifyAdminBulkUploadStudents(
            int adminUserId,
            string summaryMessage
        );

        void NotifyAdminBulkUploadTeachers(
            int adminUserId,
            string summaryMessage
        );

        // Admin changed a user's account status (suspend / reactivate / other)
        void NotifyAdminChangedUserStatus(
            int adminUserId,
            int targetUserId,
            string targetLabel,
            string roleLabel,
            string newStatus
        );

        // ============================
        // CURRICULUM: PROGRAMS & COURSES
        // ============================

        // Programs
        void NotifyAdminCreatedProgram(
            int adminUserId,
            string programCode,
            string programName
        );

        void NotifyAdminUpdatedProgram(
            int adminUserId,
            string programCode,
            string programName,
            bool isActive
        );

        void NotifyAdminDeletedProgram(
            int adminUserId,
            string programCode,
            string programName,
            bool forceDelete
        );

        // Courses
        void NotifyAdminCreatedCourse(
            int adminUserId,
            string courseCode,
            string courseTitle
        );

        void NotifyAdminUpdatedCourse(
            int adminUserId,
            string courseCode,
            string courseTitle
        );

        void NotifyAdminDeletedCourse(
            int adminUserId,
            string courseCode,
            string courseTitle
        );

        // ============================
        // ASSIGNED COURSES & ENROLLMENTS
        // ============================

        // Admin: My Activity when creating assigned course
        void NotifyAdminCreatedAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode,
            string teacherLabel
        );

        // Teacher: Updates when admin assigns them
        void NotifyTeacherAssignedToCourse(
            int adminUserId,
            int teacherUserId,
            string edpCode,
            string courseCode,
            string? semester,
            string? schoolYear
        );

        // Admin: My Activity when adding students
        void NotifyAdminAddedStudentsToAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode,
            int count
        );

        // Student: Updates when admin assigns them into an assigned course
        void NotifyStudentAddedToAssignedCourse(
            int adminUserId,
            int studentUserId,
            string edpCode,
            string courseCode,
            string? semester,
            string? schoolYear
        );

        // Admin: My Activity when updating assigned course (general)
        void NotifyAdminUpdatedAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode
        );

        // Admin: My Activity when removing students
        void NotifyAdminRemovedStudentsFromAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode,
            int count
        );

        // Admin: My Activity when deleting assigned course
        void NotifyAdminDeletedAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode
        );

        // ============================
        // GRADES
        // ============================
        void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel);
        void NotifyTeacherGradeUploaded(int teacherUserId, string courseCode, string termLabel);

        // ============================
        // EVENTS (user’s own actions on their calendar)
        // ============================
        void NotifyUserEventCreated(int ownerUserId, string title, DateTime startLocal, int actorUserId);
        void NotifyUserEventUpdated(int ownerUserId, string title, DateTime? startLocal, int actorUserId);
        void NotifyUserEventDeleted(int ownerUserId, string title, int actorUserId);

        // Core add
        void AddNotification(
            int userId,
            string title,
            string message,
            NotificationKind kind = NotificationKind.System,
            string? category = null,
            int? actorUserId = null);

        // Listing
        List<NotificationListItemVm> GetLatest(int userId, int page = 1, int pageSize = 50);
        List<NotificationListItemVm> GetLatestSystem(int userId, int take = 10);
        List<NotificationListItemVm> GetLatestActivity(int userId, int take = 10);

        // State changes
        void MarkRead(int userId, int notificationId);
        void MarkAllRead(int userId);

        // Bell
        int GetBellUnreadCount(int userId);
    }
}
