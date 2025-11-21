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

        // Always stored as UTC instants (timestamptz in DB)
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }

        public bool IsAllDay { get; set; }
        public bool IsGlobal { get; set; }
        public bool CanEdit { get; set; }

        // ✅ NEW: floating local dates for true all-day events
        // These let the UI show exact saved local dates (no guessing)
        public DateOnly? LocalStartDate { get; set; }
        public DateOnly? LocalEndDate { get; set; }

        // ✅ NEW (optional but good): timezone used for conversion
        public string TimeZoneId { get; set; } = "Asia/Manila";
    }

    // Inputs use DateTimeOffset so MVC binds ISO8601 Z/offset cleanly
    public class CalendarEventCreateVm
    {
        [Required] public string Title { get; set; }
        public string Location { get; set; }

        // Required because JS fills hidden StartUtc/EndUtc before submit,
        // even for all-day events.
        [Required] public DateTimeOffset StartUtc { get; set; }
        [Required] public DateTimeOffset EndUtc { get; set; }

        public bool IsAllDay { get; set; }

        // Floating local dates for all-day events.
        // When IsAllDay = true, JS posts these and service uses them.
        public DateOnly? LocalStartDate { get; set; }
        public DateOnly? LocalEndDate { get; set; }

        // Optional for Admin
        public bool IsGlobal { get; set; }

        // The timezone context of the event (default Manila)
        public string TimeZoneId { get; set; } = "Asia/Manila";
    }

    public class CalendarEventUpdateVm : CalendarEventCreateVm
    {
        [Required] public int Id { get; set; }
    }
}
