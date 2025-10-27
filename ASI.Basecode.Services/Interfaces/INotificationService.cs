using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Services.Interfaces
{
    public interface INotificationService
    {
        void NotifyProfileUpdated(int userId);
        void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel);

        void NotifyTeacherGradeUploaded(int teacherUserId, string courseCode, string termLabel);
        void AddNotification(
           int userId,
           string title,
           string message,
           NotificationKind kind = NotificationKind.System,
           string? category = null,
           int? actorUserId = null);


        List<NotificationListItemVm> GetLatest(int userId, int page = 1, int pageSize = 50);
        void MarkRead(int userId, int notificationId);
        void MarkAllRead(int userId);


        List<NotificationListItemVm> GetLatestSystem(int userId, int take = 10);
        List<NotificationListItemVm> GetLatestActivity(int userId, int take = 10);
    }

}
