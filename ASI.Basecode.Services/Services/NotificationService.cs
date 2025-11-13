using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repo;
        private readonly IUnitOfWork _uow;

        public NotificationService(INotificationRepository repo, IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        // ======================================================
        // PROFILE
        // ======================================================
        public void NotifyProfileUpdated(int userId)
        {
            // User updated their own profile -> My Activity
            AddNotification(
                userId: userId,
                title: "Profile updated",
                message: "Your profile information has been updated.",
                // leave kind as default (System),
                // actorUserId == userId will convert it to Activity
                category: "Profile",
                actorUserId: userId
            );
        }

        // ======================================================
        // GRADES
        // ======================================================
        public void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel)
        {
            // Student receives an UPDATE (external to them)
            AddNotification(
                userId: studentUserId,
                title: "Grade uploaded",
                message: $"Your grade for the course {courseCode} has been uploaded for {termLabel}.",
                kind: NotificationKind.System,
                category: "Grades",
                actorUserId: null // teacher/system
            );
        }

        public void NotifyTeacherGradeUploaded(int teacherUserId, string courseCode, string termLabel)
        {
            // Teacher receives MY ACTIVITY (they did the action)
            AddNotification(
                userId: teacherUserId,
                title: "Grade uploaded",
                message: $"You have uploaded grades for the course {courseCode} for the {termLabel} term.",
                // we can explicitly mark as Activity here
                kind: NotificationKind.Activity,
                category: "Grades",
                actorUserId: teacherUserId
            );
        }

        // ======================================================
        // EVENTS (user's own calendar actions)
        // ======================================================

        /// <summary>
        /// User (student/teacher) created their OWN event.
        /// Should appear under "My Activity".
        ///</summary>
        public void NotifyUserEventCreated(int ownerUserId, string title, DateTime startLocal, int actorUserId)
        {
            AddNotification(
                userId: ownerUserId,
                title: "Event created",
                message: $"You created an event \"{title}\" on {startLocal:MMM dd, yyyy}.",
                // kind left as default System; auto-converted to Activity
                // because actorUserId == userId
                category: "Events",
                actorUserId: actorUserId
            );
        }

        /// <summary>
        /// User updated their OWN event.
        /// Should appear under "My Activity".
        ///</summary>
        public void NotifyUserEventUpdated(int ownerUserId, string title, DateTime? startLocal, int actorUserId)
        {
            var detail = startLocal.HasValue
                ? $" scheduled on {startLocal.Value:MMM dd, yyyy}"
                : string.Empty;

            AddNotification(
                userId: ownerUserId,
                title: "Event updated",
                message: $"You updated your event \"{title}\"{detail}.",
                category: "Events",
                actorUserId: actorUserId
            );
        }

        /// <summary>
        /// User deleted their OWN event.
        /// Should appear under "My Activity".
        /// </summary>
        public void NotifyUserEventDeleted(int ownerUserId, string title, int actorUserId)
        {
            AddNotification(
                userId: ownerUserId,
                title: "Event deleted",
                message: $"You deleted your event \"{title}\".",
                category: "Events",
                actorUserId: actorUserId
            );
        }

        // ======================================================
        // CORE ADD LOGIC (AUTO My Activity vs Updates)
        // ======================================================
        public void AddNotification(
            int userId,
            string title,
            string message,
            NotificationKind kind = NotificationKind.System,
            string? category = null,
            int? actorUserId = null)
        {
            // 🔑 Rule:
            // - If actorUserId == userId and kind is still System (default),
            //   we treat this as "My Activity".
            // - Otherwise we keep whatever kind was passed in.
            var finalKind = kind;

            if (actorUserId.HasValue &&
                actorUserId.Value == userId &&
                kind == NotificationKind.System)
            {
                finalKind = NotificationKind.Activity;
            }

            _repo.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false,
                Kind = finalKind,
                Category = category,
                ActorUserId = actorUserId,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });

            // Keep your existing pattern:
            // SaveChanges is handled by the calling unit of work / service.
        }

        // ======================================================
        // LIST / QUERY
        // ======================================================
        public List<NotificationListItemVm> GetLatest(int userId, int page = 1, int pageSize = 50)
        {
            CleanupOldNotificationsCore(userId, retentionDays: 90, keepLast: 100);
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return _repo.GetByUser(userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationListItemVm
                {
                    Id = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    When = n.CreatedAt.ToString("MMM dd, yyyy • h:mm tt"),
                    Kind = n.Kind,
                    Category = n.Category
                })
                .ToList();
        }

        public List<NotificationListItemVm> GetLatestSystem(int userId, int take = 10)
        {
            return _repo.GetByUserAndKind(userId, NotificationKind.System)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new NotificationListItemVm
                {
                    Id = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    When = n.CreatedAt.ToString("MMM dd, yyyy • h:mm tt"),
                    Kind = n.Kind,
                    Category = n.Category
                })
                .ToList();
        }

        public List<NotificationListItemVm> GetLatestActivity(int userId, int take = 10)
        {
            return _repo.GetByUserAndKind(userId, NotificationKind.Activity)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new NotificationListItemVm
                {
                    Id = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    When = n.CreatedAt.ToString("MMM dd, yyyy • h:mm tt"),
                    Kind = n.Kind,
                    Category = n.Category
                })
                .ToList();
        }

        // ======================================================
        // STATE CHANGES
        // ======================================================
        public void MarkRead(int userId, int notificationId)
        {
            var n = _repo.GetByUser(userId).FirstOrDefault(x => x.NotificationId == notificationId);
            if (n == null) return;
            n.IsRead = true;
            _uow.SaveChanges();
        }

        public void MarkAllRead(int userId)
        {
            var list = _repo.GetByUser(userId).Where(x => !x.IsRead).ToList();
            if (list.Count == 0) return;
            foreach (var n in list) n.IsRead = true;
            _uow.SaveChanges();
        }

        // ======================================================
        // HOUSEKEEPING
        // ======================================================
        private int CleanupOldNotificationsCore(int userId, int retentionDays = 90, int keepLast = 100)
        {
            var cutoff = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-retentionDays), DateTimeKind.Unspecified);

            var oldOnes = _repo.GetAll()
                .Where(n => n.UserId == userId && !n.IsDeleted && n.CreatedAt < cutoff)
                .ToList();

            var overQuota = _repo.GetAll()
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .Skip(keepLast)
                .ToList();

            if (oldOnes.Count == 0 && overQuota.Count == 0)
                return 0;

            foreach (var n in oldOnes.Concat(overQuota).Distinct())
                n.IsDeleted = true;

            _uow.SaveChanges();
            return oldOnes.Count + overQuota.Count;
        }

        // ======================================================
        // (Optional extra) BELL DOT
        // ======================================================
        public int GetBellUnreadCount(int userId)
        {
            // Bell shows unread "Updates" only (System)
            return _repo.CountUnreadByKind(userId, NotificationKind.System);
        }
    }
}
