using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Threading.Tasks;
using System;
using System.Linq;


namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    [Route("api/events")]
    [ApiController]
    public class EventsController : ControllerBase<EventsController>  // or Controller if simpler
    {
        private readonly ICalendarService _calendar;

        public EventsController(
            ICalendarService calendar,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            AutoMapper.IMapper mapper)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _calendar = calendar;
        }

        // GET /api/events?start=2025-10-01&end=2025-11-01
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string start, [FromQuery] string end)
        {
            // FullCalendar gives ISO dates. Parse with invariant.
            var fromUtc = DateTime.Parse(start, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var toUtc = DateTime.Parse(end, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            var vm = await _calendar.GetUserCalendarAsync(User, fromUtc, toUtc);
            var payload = vm.Events.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                start = e.StartUtc.ToString("o"), // ISO
                end = e.EndUtc.ToString("o"),
                allDay = e.IsAllDay,
                extendedProps = new { location = e.Location }
            });

            return Ok(payload);
        }

        // POST /api/events  (Admin can create global; others create own)
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CalendarEventVm dto)
        {
            var isAdmin = User.IsInRole("Admin");
            var created = await _calendar.CreateAsync(User, dto, isAdminCreatesGlobal: isAdmin);
            return Ok(new { id = created.Id });
        }

        // PUT /api/events/{id}
        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CalendarEventVm dto)
        {
            dto.Id = id;
            var updated = await _calendar.UpdateAsync(User, dto);
            if (updated == null) return Forbid();
            return Ok();
        }

        // DELETE /api/events/{id}
        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _calendar.DeleteAsync(User, id);
            return Ok();
        }
    }
}
