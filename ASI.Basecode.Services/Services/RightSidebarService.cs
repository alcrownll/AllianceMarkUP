using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
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

                // Notifications
                vm.Notifications = _notifs.GetByUser(userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(takeNotifications)
                    .Select(n => new NotificationItemVm
                    {
                        Id = n.NotificationId,
                        Title = n.Title,
                        Snippet = n.Message,
                        When = n.CreatedAt.ToString("MM/dd/yy"),
                        IsRead = n.IsRead
                    })
                    .ToList();

                // Upcoming events — ALWAYS use UTC for timestamptz parameters
                var fromUtc = DateTime.UtcNow;
                var toUtc = fromUtc.AddDays(30);

                vm.UpcomingEvents = _calendar
                    .GetByUserInRange(userId, fromUtc, toUtc, includeGlobal: true)
                    .OrderBy(e => e.StartUtc)
                    .Take(takeEvents)
                    .Select(e => new UpcomingEventItemVm
                    {
                        Id = e.CalendarEventId,
                        Title = e.Title,
                        When = e.StartUtc.ToLocalTime().ToString("MM/dd/yy"),
                        WhenLocal = e.StartUtc.ToLocalTime(),
                        Location = e.Location
                    })
                    .ToList();
            }
            else
            {
                vm.LastName = user.FindFirst(ClaimTypes.Surname)?.Value
                           ?? user.FindFirst("LastName")?.Value
                           ?? "User";
                vm.Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
                vm.Notifications = new System.Collections.Generic.List<NotificationItemVm>();
                vm.UpcomingEvents = new System.Collections.Generic.List<UpcomingEventItemVm>();
            }

            return Task.FromResult(vm);
        }
    }
}
