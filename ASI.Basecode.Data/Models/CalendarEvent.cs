using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Models
{
    public class CalendarEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CalendarEventId { get; set; }

        /// <summary>
        /// Owner of a private/personal event. NULL when the event is GLOBAL.
        /// </summary>
        public int? UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; }

        /// <summary>
        /// Who created the row (for audit).
        /// </summary>
        [Required]
        public int CreatedByUserId { get; set; }
        [ForeignKey(nameof(CreatedByUserId))]
        public User CreatedBy { get; set; }

        /// <summary>
        /// True => global event (visible to all roles). Only Admin can set this.
        /// </summary>
        public bool IsGlobal { get; set; } = false;

        [Required, MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(200)]
        public string Location { get; set; }

        /// <summary>
        /// The event's local time zone (IANA), e.g. "Asia/Manila".
        /// Used for correct display and recurrence logic.
        /// </summary>
        [Required, MaxLength(100)]
        public string TimeZoneId { get; set; } = "Asia/Manila";

        /// <summary>
        /// Point-in-time boundaries (always UTC). Map to timestamptz.
        /// </summary>
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }

        /// <summary>
        /// True for all-day (floating) events. When true, LocalStartDate/LocalEndDate are used for rendering.
        /// </summary>
        public bool IsAllDay { get; set; }

        /// <summary>
        /// Local calendar dates for all-day events (floating dates, not instants).
        /// When IsAllDay = true, fill these and still keep StartUtc/EndUtc for alarms if you want.
        /// </summary>
        public DateOnly? LocalStartDate { get; set; }
        public DateOnly? LocalEndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
