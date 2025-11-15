using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class RightSidebarViewModel
    {
        public string LastName { get; set; }
        public string Role { get; set; }
        public string ProfilePictureUrl { get; set; }

        public List<NotificationItemVm> Notifications { get; set; } = new();
        public int UnreadUpdatesCount { get; set; }

        public List<UpcomingEventItemVm> UpcomingEvents { get; set; } = new();

      
        public HashSet<string> UserEventDates { get; set; } = new();
        public HashSet<string> GlobalEventDates { get; set; } = new();
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
        public string When { get; set; }          // e.g., "11/13/25"
        public DateTime WhenLocal { get; set; } //disregard later ig
        public string Location { get; set; }

   
        public bool IsGlobal { get; set; }
    }
}
