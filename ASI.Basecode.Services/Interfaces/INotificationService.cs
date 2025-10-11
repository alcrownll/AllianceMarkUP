using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface INotificationService
    {
        void NotifyProfileUpdated(int userId);
        void NotifyGradesPosted(int studentUserId, string courseCode, string termLabel);

        // Queries / state changes for the page
        List<NotificationListItemVm> GetLatest(int userId, int page = 1, int pageSize = 50);
        void MarkRead(int userId, int notificationId);
        void MarkAllRead(int userId);
    }

}
