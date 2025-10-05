using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface ICalendarEventRepository
    {
        IQueryable<CalendarEvent> GetByUserInRange(int userId, DateTime startUtc, DateTime endUtc, bool includeGlobal = true);
        CalendarEvent GetNextForUser(int userId, DateTime fromLocal, bool includeGlobal = true);
        IQueryable<CalendarEvent> GetUpcomingForUser(int userId, DateTime fromLocal, DateTime toLocal, bool includeGlobal = true);

        CalendarEvent GetById(int id);
        void Add(CalendarEvent e);
        void Update(CalendarEvent e);
        void Delete(int id);
    }
}
