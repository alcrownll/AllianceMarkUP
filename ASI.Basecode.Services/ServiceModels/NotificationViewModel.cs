using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class NotificationListItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public string When { get; set; }

        public NotificationKind Kind { get; set; }
        public string Category { get; set; }
    }

    // Payload for the page
    public class NotificationPageVm
    {
        public List<NotificationListItemVm> Items { get; set; } = new();
        public int UnreadCount { get; set; }
    }

    public class ActivityItemVm
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string When { get; set; } = "";
        public string? ActionType { get; set; } 
    }
}
