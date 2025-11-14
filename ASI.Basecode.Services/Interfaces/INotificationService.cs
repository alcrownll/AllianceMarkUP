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

        // 🔹 Admin changed a user's account status (suspend / reactivate / other)
        void NotifyAdminChangedUserStatus(
            int adminUserId,
            int targetUserId,
            string targetLabel,  
            string roleLabel,   
            string newStatus     
        );

        // Grades
        void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel);
        void NotifyTeacherGradeUploaded(int teacherUserId, string courseCode, string termLabel);

        // Events (user’s own actions on their calendar)
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
