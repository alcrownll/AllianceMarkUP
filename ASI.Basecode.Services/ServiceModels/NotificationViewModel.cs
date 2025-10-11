using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    // One row in the list
    public class NotificationListItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public string When { get; set; }  // e.g., "Oct 10, 2025 • 3:41 PM"
    }

    // Payload for the page
    public class NotificationPageVm
    {
        public List<NotificationListItemVm> Items { get; set; } = new();
        public int UnreadCount { get; set; }
    }
}
