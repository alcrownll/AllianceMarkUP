using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class NotificationRepository : BaseRepository, INotificationRepository
    {
        public NotificationRepository(IUnitOfWork uow) : base(uow) { }

        public IQueryable<Notification> GetAll() => this.GetDbSet<Notification>();
        public IQueryable<Notification> GetByUser(int userId)
        => GetDbSet<Notification>()
         .Where(n => n.UserId == userId && !n.IsDeleted);

        public void Add(Notification n)
        {
            GetDbSet<Notification>().Add(n);
            UnitOfWork.SaveChanges();
        }

        public void MarkRead(int notificationId)
        {
            var n = GetDbSet<Notification>().FirstOrDefault(x => x.NotificationId == notificationId);
            if (n != null)
            {
                n.IsRead = true;
                UnitOfWork.SaveChanges();
            }
        }
    }
}
