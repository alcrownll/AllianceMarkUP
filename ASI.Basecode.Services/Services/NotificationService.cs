using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork _uow;

    public NotificationService(INotificationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public void NotifyProfileUpdated(int userId)
        => Add(userId, "Profile updated", "Your profile information has been updated.");

    public void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel)
        => Add(studentUserId, "Grade updated", $"Your grade for {courseCode} was updated ({termLabel}).");

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
                When = n.CreatedAt.ToString("MMM dd, yyyy • h:mm tt")
            })
            .ToList();
    }

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

    private void Add(int userId, string title, string message)
    {
        _repo.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            IsRead = false,
            // Your DB column is "timestamp without time zone" → use Unspecified
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        _uow.SaveChanges();
    }

    private int CleanupOldNotificationsCore(int userId, int retentionDays = 90, int keepLast = 100)
    {
        // cutoff for age
        var cutoff = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-retentionDays), DateTimeKind.Unspecified);

        // 1) Old ones by time (for this user)
        var oldOnes = _repo.GetAll()
            .Where(n => n.UserId == userId && !n.IsDeleted && n.CreatedAt < cutoff)
            .ToList();

        // 2) Over-quota (keep only most recent N for this user)
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
}
