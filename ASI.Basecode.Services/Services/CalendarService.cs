using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class CalendarService : ICalendarService
    {
        private readonly ICalendarEventRepository _events;
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _users;   // 👈 NEW: for global recipients

        public CalendarService(
            ICalendarEventRepository eventsRepo,
            INotificationService notificationService,
            IUserRepository users)
        {
            _events = eventsRepo;
            _notificationService = notificationService;
            _users = users;
        }

        private static bool CanEdit(ClaimsPrincipal principal, CalendarEvent e, int actorId)
        {
            if (principal.IsInRole("Admin")) return true;
            // Only owner of a private event can edit it
            return e.IsGlobal == false && e.UserId == actorId;
        }

        public Task<CalendarViewModel> GetUserCalendarAsync(ClaimsPrincipal principal, DateTime fromUtc, DateTime toUtc)
        {
            int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId);

            var items = _events.GetByUserInRange(userId, fromUtc, toUtc, includeGlobal: true)
                .OrderBy(e => e.StartUtc)
                .Select(e => new CalendarEventVm
                {
                    Id = e.CalendarEventId,
                    Title = e.Title,
                    Location = e.Location,
                    StartUtc = e.StartUtc,
                    EndUtc = e.EndUtc,
                    IsAllDay = e.IsAllDay,
                    IsGlobal = e.IsGlobal,
                    CanEdit = CanEdit(principal, e, userId)
                })
                .ToList();

            return Task.FromResult(new CalendarViewModel { Events = items });
        }

        public Task<CalendarEventVm> CreateAsync(ClaimsPrincipal principal, CalendarEventCreateVm input)
        {
            int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);
            var isAdmin = principal.IsInRole("Admin");

            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.StartUtc == default || input.EndUtc == default)
                throw new ArgumentException("Start and End are required.");
            if (input.EndUtc < input.StartUtc)
                throw new ArgumentException("End time must be after start time.");

            var startUtc = DateTime.SpecifyKind(input.StartUtc.ToUniversalTime().UtcDateTime, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(input.EndUtc.ToUniversalTime().UtcDateTime, DateTimeKind.Utc);

            var tz = string.IsNullOrWhiteSpace(input.TimeZoneId) ? "Asia/Manila" : input.TimeZoneId;

            // ADMIN can decide if it's global; others always create personal events
            var willBeGlobal = isAdmin && input.IsGlobal;
            var ownerUserId = willBeGlobal ? (int?)null : actorId;

            var entity = new CalendarEvent
            {
                Title = input.Title?.Trim(),
                Location = input.Location?.Trim(),
                StartUtc = startUtc,
                EndUtc = endUtc,
                IsAllDay = input.IsAllDay,
                TimeZoneId = tz,
                LocalStartDate = input.IsAllDay ? input.LocalStartDate : null,
                LocalEndDate = input.IsAllDay ? (input.LocalEndDate ?? input.LocalStartDate) : null,
                IsGlobal = willBeGlobal,
                UserId = ownerUserId,
                CreatedByUserId = actorId,
                CreatedAt = DateTime.UtcNow
            };

            _events.Add(entity);

            // Build a safe DateTime representation for notifications
            DateTime startLocalForNotif;
            if (entity.IsAllDay && entity.LocalStartDate.HasValue)
            {
                var d = entity.LocalStartDate.Value;  // DateOnly or DateTime, both support Year/Month/Day
                startLocalForNotif = new DateTime(d.Year, d.Month, d.Day);
            }
            else
            {
                startLocalForNotif = entity.StartUtc.ToLocalTime();
            }

            // ---------- NOTIFICATIONS ----------
            if (willBeGlobal && isAdmin)
            {
                // 1) Admin's own "My Activity" (optional but nice)
                if (actorId > 0)
                {
                    _notificationService.NotifyUserEventCreated(
                        ownerUserId: actorId,
                        title: entity.Title,
                        startLocal: startLocalForNotif,
                        actorUserId: actorId
                    );
                }

                // 2) System Updates for all Students + Teachers
                var recipients = _users.GetUsers()
                    .Where(u => (u.Role == "Student" || u.Role == "Teacher") && u.UserId != actorId)
                    .Select(u => u.UserId)
                    .ToList();

                foreach (var uid in recipients)
                {
                    _notificationService.AddNotification(
                        userId: uid,
                        title: "New global event",
                        message: $"A new global event \"{entity.Title}\" is scheduled on {startLocalForNotif:MMM dd, yyyy}.",
                        kind: NotificationKind.System,        // stays in Updates
                        category: "Events",
                        actorUserId: actorId                  // admin is the actor
                    );
                }
            }
            else
            {
                // Non-global event: this is the actor's own personal event → My Activity
                if (ownerUserId.HasValue && ownerUserId.Value > 0)
                {
                    _notificationService.NotifyUserEventCreated(
                        ownerUserId: ownerUserId.Value,
                        title: entity.Title,
                        startLocal: startLocalForNotif,
                        actorUserId: actorId
                    );
                }
            }

            return Task.FromResult(new CalendarEventVm
            {
                Id = entity.CalendarEventId,
                Title = entity.Title,
                Location = entity.Location,
                StartUtc = entity.StartUtc,
                EndUtc = entity.EndUtc,
                IsAllDay = entity.IsAllDay,
                IsGlobal = entity.IsGlobal,
                CanEdit = true
            });
        }

        public Task<CalendarEventVm> UpdateAsync(ClaimsPrincipal principal, CalendarEventUpdateVm input)
        {
            int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);
            var isAdmin = principal.IsInRole("Admin");
            if (input == null) return Task.FromResult<CalendarEventVm>(null);

            var e = _events.GetById(input.Id);
            if (e == null) return Task.FromResult<CalendarEventVm>(null);
            if (!CanEdit(principal, e, actorId)) return Task.FromResult<CalendarEventVm>(null);

            if (input.StartUtc == default || input.EndUtc == default)
                throw new ArgumentException("Start and End are required.");
            if (input.EndUtc < input.StartUtc)
                throw new ArgumentException("End time must be after start time.");

            e.Title = input.Title?.Trim();
            e.Location = input.Location?.Trim();
            e.StartUtc = DateTime.SpecifyKind(input.StartUtc.UtcDateTime, DateTimeKind.Utc);
            e.EndUtc = DateTime.SpecifyKind(input.EndUtc.UtcDateTime, DateTimeKind.Utc);
            e.IsAllDay = input.IsAllDay;

            if (isAdmin)
            {
                e.IsGlobal = input.IsGlobal;
                e.UserId = e.IsGlobal ? (int?)null : actorId;
            }

            e.TimeZoneId = string.IsNullOrWhiteSpace(input.TimeZoneId)
                ? (e.TimeZoneId ?? "Asia/Manila")
                : input.TimeZoneId;

            if (input.IsAllDay)
            {
                e.LocalStartDate = input.LocalStartDate;
                e.LocalEndDate = input.LocalEndDate ?? input.LocalStartDate;
            }
            else
            {
                e.LocalStartDate = null;
                e.LocalEndDate = null;
            }

            e.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            _events.Update(e);

            // For now, only notify the actor about their own activity.
            // (You can later add global update broadcasts if you want.)
            DateTime startLocalForNotif;
            if (e.IsAllDay && e.LocalStartDate.HasValue)
            {
                var d = e.LocalStartDate.Value;
                startLocalForNotif = new DateTime(d.Year, d.Month, d.Day);
            }
            else
            {
                startLocalForNotif = e.StartUtc.ToLocalTime();
            }

            _notificationService.NotifyUserEventUpdated(
                ownerUserId: actorId,
                title: e.Title,
                startLocal: startLocalForNotif,
                actorUserId: actorId
            );

            return Task.FromResult(new CalendarEventVm
            {
                Id = e.CalendarEventId,
                Title = e.Title,
                Location = e.Location,
                StartUtc = e.StartUtc,
                EndUtc = e.EndUtc,
                IsAllDay = e.IsAllDay,
                IsGlobal = e.IsGlobal,
                CanEdit = true
            });
        }

        public Task DeleteAsync(ClaimsPrincipal principal, int id)
        {
            int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId);
            var e = _events.GetById(id);
            if (e == null) return Task.CompletedTask;
            if (!CanEdit(principal, e, actorId)) return Task.CompletedTask;

            var title = e.Title; // capture before delete
            _events.Delete(id);

            // Actor’s own My Activity
            _notificationService.NotifyUserEventDeleted(
                ownerUserId: actorId,
                title: title,
                actorUserId: actorId
            );

            return Task.CompletedTask;
        }
    }
}
