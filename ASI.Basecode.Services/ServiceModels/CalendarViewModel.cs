using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class CalendarViewModel
    {
        public List<CalendarEventVm> Events { get; set; } = new();
    }

    public class CalendarEventVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public DateTime StartUtc { get; set; }   // stored/displayed as UTC
        public DateTime EndUtc { get; set; }     // stored/displayed as UTC
        public bool IsAllDay { get; set; }
        public bool IsGlobal { get; set; }
        public bool CanEdit { get; set; }
    }

    // Inputs use DateTimeOffset so MVC binds ISO8601 Z/offset cleanly
    public class CalendarEventCreateVm
    {
        [Required] public string Title { get; set; }
        public string Location { get; set; }

        // MUST be DateTimeOffset so "2025-10-11T08:00:00Z" binds safely
        [Required] public DateTimeOffset StartUtc { get; set; }
        [Required] public DateTimeOffset EndUtc { get; set; }

        public bool IsAllDay { get; set; }
        public DateOnly? LocalStartDate { get; set; }
        public DateOnly? LocalEndDate { get; set; }

        // Optional for Admin
        public bool IsGlobal { get; set; }
        public string TimeZoneId { get; set; } = "Asia/Manila";
    }

    public class CalendarEventUpdateVm : CalendarEventCreateVm
    {
        [Required] public int Id { get; set; }
    }
}
