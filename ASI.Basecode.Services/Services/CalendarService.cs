using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
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
    public class CalendarService : ICalendarService
    {
        private readonly ICalendarEventRepository _events;

        public CalendarService(ICalendarEventRepository eventsRepo)
        {
            _events = eventsRepo;
        }

        public Task<CalendarViewModel> GetUserCalendarAsync(ClaimsPrincipal user, DateTime fromUtc, DateTime toUtc)
        {
            int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId);
            var items = _events.GetByUserInRange(userId, fromUtc, toUtc, includeGlobal: true)
                               .OrderBy(e => e.StartUtc)
                               .Select(e => new CalendarEventVm
                               {
                                   Id = e.CalendarEventId,
                                   Title = e.Title,
                                   Location = e.Location,
                                   StartUtc = e.StartUtc,
                                   EndUtc = e.EndUtc,
                                   IsAllDay = e.IsAllDay
                               }).ToList();

            return Task.FromResult(new CalendarViewModel { Events = items });
        }

        public Task<CalendarEventVm> CreateAsync(ClaimsPrincipal user, CalendarEventVm dto, bool isAdminCreatesGlobal)
        {
            int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId);

            var entity = new CalendarEvent
            {
                Title = dto.Title?.Trim(),
                Location = dto.Location?.Trim(),
                StartUtc = dto.StartUtc,
                EndUtc = dto.EndUtc,
                IsAllDay = dto.IsAllDay,
                UserId = isAdminCreatesGlobal ? (int?)null : userId
            };
            _events.Add(entity);

            dto.Id = entity.CalendarEventId;
            return Task.FromResult(dto);
        }

        public Task<CalendarEventVm> UpdateAsync(ClaimsPrincipal user, CalendarEventVm dto)
        {
            int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId);
            var e = _events.GetById(dto.Id);
            if (e == null) return Task.FromResult<CalendarEventVm>(null);

            // Authorization: allow owner or admin to edit; admins can edit global/null-user events
            var isAdmin = user.IsInRole("Admin");
            if (!isAdmin && e.UserId != userId) return Task.FromResult<CalendarEventVm>(null);

            e.Title = dto.Title?.Trim();
            e.Location = dto.Location?.Trim();
            e.StartUtc = dto.StartUtc;
            e.EndUtc = dto.EndUtc;
            e.IsAllDay = dto.IsAllDay;
            e.UpdatedAt = DateTime.UtcNow;
            _events.Update(e);

            return Task.FromResult(dto);
        }

        public Task DeleteAsync(ClaimsPrincipal user, int id)
        {
            int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId);
            var e = _events.GetById(id);
            if (e == null) return Task.CompletedTask;

            var isAdmin = user.IsInRole("Admin");
            if (!isAdmin && e.UserId != userId) return Task.CompletedTask;

            _events.Delete(id);
            return Task.CompletedTask;
        }
    }
}
