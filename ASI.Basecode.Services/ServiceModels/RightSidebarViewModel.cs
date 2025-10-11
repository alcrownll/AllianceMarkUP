using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class RightSidebarViewModel
    {
        public string LastName { get; set; }
        public string Role { get; set; }
        public string ProfilePictureUrl { get; set; }
        public List<NotificationItemVm> Notifications { get; set; } = new();
        public List<UpcomingEventItemVm> UpcomingEvents { get; set; } = new();
    }

    public class NotificationItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string When { get; set; }
        public bool IsRead { get; set; }
    }

public class UpcomingEventItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string When { get; set; }        // e.g. "03/05/25"
        public DateTime WhenLocal { get; set; } // optional if you need DateTime
        public string Location { get; set; }
    }

}
