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
        private readonly IUserRepository _users;

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
            return e.IsGlobal == false && e.UserId == actorId;
        }

        // Resolve timezone safely (IANA on Linux, Windows IDs on Windows)
        private static TimeZoneInfo ResolveTimeZone(string tzId)
        {
            if (string.IsNullOrWhiteSpace(tzId)) tzId = "Asia/Manila";

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                // Manila fallback for Windows machines
                if (tzId == "Asia/Manila")
                {
                    try { return TimeZoneInfo.FindSystemTimeZoneById("Philippines Standard Time"); }
                    catch { /* ignore */ }
                }

                // final fallback: local server timezone
                return TimeZoneInfo.Local;
            }
        }

        private static DateTime UtcFromLocalDate(DateOnly d, TimeZoneInfo tz)
        {
            // Local midnight (unspecified kind)
            var localMidnight = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
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

            var tzId = string.IsNullOrWhiteSpace(input.TimeZoneId) ? "Asia/Manila" : input.TimeZoneId;
            var tz = ResolveTimeZone(tzId);

            DateTime startUtc;
            DateTime endUtc;

            if (input.IsAllDay)
            {
                if (!input.LocalStartDate.HasValue)
                    throw new ArgumentException("LocalStartDate is required for all-day events.");

                var startDate = input.LocalStartDate.Value;
                var endDate = input.LocalEndDate ?? startDate;

                if (endDate < startDate)
                    throw new ArgumentException("End date must not be before start date.");

                startUtc = UtcFromLocalDate(startDate, tz);
                // exclusive end = next day's midnight
                endUtc = UtcFromLocalDate(endDate.AddDays(1), tz);
            }
            else
            {
                if (input.StartUtc == default || input.EndUtc == default)
                    throw new ArgumentException("Start and End are required.");
                if (input.EndUtc < input.StartUtc)
                    throw new ArgumentException("End time must be after start time.");

                startUtc = DateTime.SpecifyKind(input.StartUtc.ToUniversalTime().UtcDateTime, DateTimeKind.Utc);
                endUtc = DateTime.SpecifyKind(input.EndUtc.ToUniversalTime().UtcDateTime, DateTimeKind.Utc);
            }

            var willBeGlobal = isAdmin && input.IsGlobal;
            var ownerUserId = willBeGlobal ? (int?)null : actorId;

            var entity = new CalendarEvent
            {
                Title = input.Title?.Trim(),
                Location = input.Location?.Trim(),
                StartUtc = startUtc,
                EndUtc = endUtc,
                IsAllDay = input.IsAllDay,
                TimeZoneId = tzId,
                LocalStartDate = input.IsAllDay ? input.LocalStartDate : null,
                LocalEndDate = input.IsAllDay ? (input.LocalEndDate ?? input.LocalStartDate) : null,
                IsGlobal = willBeGlobal,
                UserId = ownerUserId,
                CreatedByUserId = actorId,
                CreatedAt = DateTime.UtcNow
            };

            _events.Add(entity);

            // Notification date:
            DateTime startLocalForNotif;
            if (entity.IsAllDay && entity.LocalStartDate.HasValue)
            {
                var d = entity.LocalStartDate.Value;
                startLocalForNotif = new DateTime(d.Year, d.Month, d.Day);
            }
            else
            {
                startLocalForNotif = TimeZoneInfo.ConvertTimeFromUtc(entity.StartUtc, tz);
            }

            if (willBeGlobal && isAdmin)
            {
                if (actorId > 0)
                {
                    _notificationService.NotifyUserEventCreated(
                        ownerUserId: actorId,
                        title: entity.Title,
                        startLocal: startLocalForNotif,
                        actorUserId: actorId
                    );
                }

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
                        kind: NotificationKind.System,
                        category: "Events",
                        actorUserId: actorId
                    );
                }
            }
            else
            {
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

            var tzId = string.IsNullOrWhiteSpace(input.TimeZoneId)
                ? (e.TimeZoneId ?? "Asia/Manila")
                : input.TimeZoneId;

            var tz = ResolveTimeZone(tzId);

            DateTime startUtc;
            DateTime endUtc;

            if (input.IsAllDay)
            {
                if (!input.LocalStartDate.HasValue)
                    throw new ArgumentException("LocalStartDate is required for all-day events.");

                var startDate = input.LocalStartDate.Value;
                var endDate = input.LocalEndDate ?? startDate;

                if (endDate < startDate)
                    throw new ArgumentException("End date must not be before start date.");

                startUtc = UtcFromLocalDate(startDate, tz);
                endUtc = UtcFromLocalDate(endDate.AddDays(1), tz);
            }
            else
            {
                if (input.StartUtc == default || input.EndUtc == default)
                    throw new ArgumentException("Start and End are required.");
                if (input.EndUtc < input.StartUtc)
                    throw new ArgumentException("End time must be after start time.");

                startUtc = DateTime.SpecifyKind(input.StartUtc.ToUniversalTime().UtcDateTime, DateTimeKind.Utc);
                endUtc = DateTime.SpecifyKind(input.EndUtc.ToUniversalTime().UtcDateTime, DateTimeKind.Utc);
            }

            e.Title = input.Title?.Trim();
            e.Location = input.Location?.Trim();
            e.StartUtc = startUtc;
            e.EndUtc = endUtc;
            e.IsAllDay = input.IsAllDay;

            if (isAdmin)
            {
                e.IsGlobal = input.IsGlobal;
                e.UserId = e.IsGlobal ? (int?)null : actorId;
            }

            e.TimeZoneId = tzId;

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

            DateTime startLocalForNotif;
            if (e.IsAllDay && e.LocalStartDate.HasValue)
            {
                var d = e.LocalStartDate.Value;
                startLocalForNotif = new DateTime(d.Year, d.Month, d.Day);
            }
            else
            {
                startLocalForNotif = TimeZoneInfo.ConvertTimeFromUtc(e.StartUtc, tz);
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

            var title = e.Title;
            _events.Delete(id);

            _notificationService.NotifyUserEventDeleted(
                ownerUserId: actorId,
                title: title,
                actorUserId: actorId
            );

            return Task.CompletedTask;
        }
    }
}
