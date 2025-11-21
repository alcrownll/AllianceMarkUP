using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface INotificationService
    {
        // Profile
        void NotifyProfileUpdated(int userId);

        void NotifyAdminUpdatedUserProfile(
            int adminUserId,
            int targetUserId,
            string targetDisplayName,
            string? targetIdNumber
        );

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

        void NotifyAdminBulkUploadStudents(
            int adminUserId,
            string summaryMessage
        );

        void NotifyAdminBulkUploadTeachers(
            int adminUserId,
            string summaryMessage
        );

        void NotifyAdminChangedUserStatus(
            int adminUserId,
            int targetUserId,
            string targetLabel,
            string roleLabel,
            string newStatus
        );

        void NotifyPasswordChanged(int userId);

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

        // Assigned courses / enrollments
        void NotifyAdminCreatedAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode,
            string teacherLabel
        );

        void NotifyTeacherAssignedToCourse(
            int adminUserId,
            int teacherUserId,
            string edpCode,
            string courseCode,
            string? semester,
            string? schoolYear
        );

        void NotifyAdminAddedStudentsToAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode,
            int count
        );

        void NotifyStudentAddedToAssignedCourse(
            int adminUserId,
            int studentUserId,
            string edpCode,
            string courseCode,
            string? semester,
            string? schoolYear
        );

        void NotifyAdminUpdatedAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode
        );

        void NotifyAdminRemovedStudentsFromAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode,
            int count
        );

        void NotifyAdminDeletedAssignedCourse(
            int adminUserId,
            string edpCode,
            string courseCode
        );

        // Grades
        void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel);
        void NotifyTeacherGradeUploaded(int teacherUserId, string courseCode, string termLabel);

        // Events
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

        void MarkAllRead(int userId, NotificationKind? kind = null);

        // Bell
        int GetBellUnreadCount(int userId);
    }
}
