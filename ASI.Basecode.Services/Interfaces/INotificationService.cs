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
    }
}
