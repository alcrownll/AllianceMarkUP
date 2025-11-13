using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class RightSidebarService : IRightSidebarService
    {
        private readonly IUserRepository _users;
        private readonly IUserProfileRepository _profiles;
        private readonly INotificationRepository _notifs;
        private readonly ICalendarEventRepository _calendar;

        public RightSidebarService(
            IUserRepository users,
            IUserProfileRepository profiles,
            INotificationRepository notifs,
            ICalendarEventRepository calendar)
        {
            _users = users;
            _profiles = profiles;
            _notifs = notifs;
            _calendar = calendar;
        }

        public Task<RightSidebarViewModel> BuildAsync(
            ClaimsPrincipal user,
            int takeNotifications = 5,
            int takeEvents = 5)
        {
            var vm = new RightSidebarViewModel();

            // Resolve userId from claims or db
            int userId = 0;
            var idStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idStr, out userId) || userId <= 0)
            {
                var idNumber = user.FindFirst("IdNumber")?.Value;
                if (!string.IsNullOrWhiteSpace(idNumber))
                {
                    userId = _users.GetUsers()
                                   .Where(u => u.IdNumber == idNumber)
                                   .Select(u => u.UserId)
                                   .FirstOrDefault();
                }
                else
                {
                    var email = user.FindFirst(ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        userId = _users.GetUsers()
                                       .Where(u => u.Email == email)
                                       .Select(u => u.UserId)
                                       .FirstOrDefault();
                    }
                }
            }

            if (userId > 0)
            {
                var dbUser = _users.GetUsers()
                                   .Where(u => u.UserId == userId)
                                   .Select(u => new { u.LastName, u.Role })
                                   .FirstOrDefault();

                vm.LastName = dbUser?.LastName
                            ?? user.FindFirst(ClaimTypes.Surname)?.Value
                            ?? user.FindFirst("LastName")?.Value
                            ?? "User";

                vm.Role = dbUser?.Role
                        ?? user.FindFirst(ClaimTypes.Role)?.Value
                        ?? "Unknown";

                vm.ProfilePictureUrl = _profiles.GetUserProfiles()
                    .Where(p => p.UserId == userId)
                    .Select(p => p.ProfilePictureUrl)
                    .FirstOrDefault();

                // Recent Activities (Activity only)
                vm.Notifications = _notifs.GetByUserAndKind(userId, NotificationKind.Activity)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(takeNotifications)
                    .Select(n => new NotificationItemVm
                    {
                        Id = n.NotificationId,
                        Title = n.Title,
                        Snippet = n.Message,
                        When = n.CreatedAt.ToLocalTime().ToString("MM/dd/yy"),
                        IsRead = n.IsRead
                    })
                    .ToList();

                // Unread system updates count for bell dot
                vm.UnreadUpdatesCount = _notifs.CountUnreadByKind(userId, NotificationKind.System);

                // Pull events (user + global) for the mini calendar + upcoming list
                var fromUtc = DateTime.UtcNow.AddMonths(-1);
                var toUtc = DateTime.UtcNow.AddMonths(3);

                var evs = _calendar
                    .GetByUserInRange(userId, fromUtc, toUtc, includeGlobal: true)
                    .ToList();

                vm.UpcomingEvents = evs
                    .OrderBy(e => e.StartUtc)
                    .Where(e => e.StartUtc >= DateTime.UtcNow)
                    .Take(takeEvents)
                    .Select(e => new UpcomingEventItemVm
                    {
                        Id = e.CalendarEventId,
                        Title = e.Title,
                        When = e.StartUtc.ToLocalTime().ToString("MM/dd/yy"),
                        WhenLocal = e.StartUtc.ToLocalTime(),
                        Location = e.Location,
                        IsGlobal = e.IsGlobal
                    })
                    .ToList();

                // === Fill both sets with EVERY local date in each event's range (supports multi-day + all-day) ===
                foreach (var e in evs)
                {
                    DateTime startLocalDate;
                    DateTime endLocalDate;

                    // Prefer all-day's LocalStart/End if present
                    if (e.IsAllDay && e.LocalStartDate.HasValue)
                    {
                        var lsd = e.LocalStartDate.Value;
                        var led = (e.LocalEndDate ?? e.LocalStartDate).GetValueOrDefault(lsd);

                        startLocalDate = new DateTime(lsd.Year, lsd.Month, lsd.Day);
                        endLocalDate = new DateTime(led.Year, led.Month, led.Day);
                    }
                    else
                    {
                        var sLocal = e.StartUtc.ToLocalTime();
                        var eLocal = (e.EndUtc == default ? e.StartUtc : e.EndUtc).ToLocalTime();

                        startLocalDate = sLocal.Date;
                        endLocalDate = eLocal.Date;
                    }

                    if (endLocalDate < startLocalDate)
                        endLocalDate = startLocalDate;

                    for (var d = startLocalDate; d <= endLocalDate; d = d.AddDays(1))
                    {
                        var iso = d.ToString("yyyy-MM-dd");
                        if (e.IsGlobal) vm.GlobalEventDates.Add(iso);
                        else vm.UserEventDates.Add(iso);
                    }
                }
            }
            else
            {
                // Anonymous fallback
                vm.LastName = user.FindFirst(ClaimTypes.Surname)?.Value
                           ?? user.FindFirst("LastName")?.Value
                           ?? "User";
                vm.Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            }

            return Task.FromResult(vm);
        }
    }
}
