using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface INotificationRepository
    {
        IQueryable<Notification> GetAll();
        IQueryable<Notification> GetByUser(int userId);
        void Add(Notification n);
        void MarkRead(int notificationId);


        IQueryable<Notification> GetByUserAndKind(int userId, NotificationKind kind);
        int CountUnreadByKind(int userId, NotificationKind kind);
        IQueryable<Notification> GetByUserAndCategory(int userId, string category);
    }
}
