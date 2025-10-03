using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class RightSidebarService : IRightSidebarService
    {
        private readonly IUserRepository _users;
        private readonly INotificationRepository _notifs;
        private readonly ICalendarEventRepository _calendar;
        public RightSidebarService(IUserRepository users, INotificationRepository notifs, ICalendarEventRepository calendar)
        {
            _users = users;
            _notifs = notifs;
            _calendar = calendar;
        }

        public Task<RightSidebarViewModel> BuildAsync(
    ClaimsPrincipal user,
    int takeNotifications = 5,
    int takeEvents = 5)
        {
            var vm = new RightSidebarViewModel();

            // ----- resolve userId (your existing logic) -----
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

                // Fallback to claims if DB lookup failed
                vm.LastName = dbUser?.LastName
                            ?? user.FindFirst(ClaimTypes.Surname)?.Value
                            ?? user.FindFirst("LastName")?.Value
                            ?? "User";

                vm.Role = dbUser?.Role
                        ?? user.FindFirst(ClaimTypes.Role)?.Value
                        ?? "Unknown";

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

                // Upcoming events
                var nowLocal = DateTime.Now;
                var fromLocal = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);
                var toLocal = DateTime.SpecifyKind(nowLocal.AddDays(30), DateTimeKind.Unspecified);

                vm.UpcomingEvents = _calendar
                    .GetByUserInRange(userId, fromLocal, toLocal, includeGlobal: true)
                    .OrderBy(e => e.StartUtc)
                    .Take(takeEvents)
                    .Select(e => new UpcomingEventItemVm
                    {
                        Id = e.CalendarEventId,
                        Title = e.Title,
                        When = e.StartUtc.ToString("MM/dd/yy"),
                        WhenLocal = e.StartUtc,
                        Location = e.Location
                    })
                    .ToList();
            }
            else
            {
                // No userId at all: claims only
                vm.LastName = user.FindFirst(ClaimTypes.Surname)?.Value
                           ?? user.FindFirst("LastName")?.Value
                           ?? "User";
                vm.Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                vm.Notifications = new List<NotificationItemVm>();
                vm.UpcomingEvents = new List<UpcomingEventItemVm>();
            }


            return Task.FromResult(vm);
        }


    }
}
