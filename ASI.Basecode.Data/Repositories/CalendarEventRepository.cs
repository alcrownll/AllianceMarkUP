using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class CalendarEventRepository : BaseRepository, ICalendarEventRepository
    {
        public CalendarEventRepository(IUnitOfWork uow) : base(uow) { }

        public IQueryable<CalendarEvent> GetByUserInRange(int userId, DateTime startUtc, DateTime endUtc, bool includeGlobal = true)
        {
            var set = GetDbSet<CalendarEvent>();
            return set.Where(e =>
                (includeGlobal ? (e.UserId == null || e.UserId == userId) : e.UserId == userId) &&
                e.EndUtc >= startUtc && e.StartUtc <= endUtc);
        }

        public CalendarEvent GetNextForUser(int userId, DateTime fromLocal, bool includeGlobal = true)
        {
            var set = GetDbSet<CalendarEvent>();
            return set
                .Where(e =>
                    (includeGlobal ? (e.UserId == null || e.UserId == userId) : e.UserId == userId) &&
                    e.StartUtc >= fromLocal)
                .OrderBy(e => e.StartUtc)
                .FirstOrDefault();
        }

        public IQueryable<CalendarEvent> GetUpcomingForUser(int userId, DateTime fromLocal, DateTime toLocal, bool includeGlobal = true)
        {
            var set = GetDbSet<CalendarEvent>();
            return set
                .Where(e =>
                    (includeGlobal ? (e.UserId == null || e.UserId == userId) : e.UserId == userId) &&
                    e.StartUtc >= fromLocal && e.StartUtc <= toLocal)
                .OrderBy(e => e.StartUtc);
        }

        public CalendarEvent GetById(int id) => GetDbSet<CalendarEvent>().FirstOrDefault(x => x.CalendarEventId == id);

        public void Add(CalendarEvent e) { GetDbSet<CalendarEvent>().Add(e); UnitOfWork.SaveChanges(); }
        public void Update(CalendarEvent e) { GetDbSet<CalendarEvent>().Update(e); UnitOfWork.SaveChanges(); }
        public void Delete(int id) { var e = GetById(id); if (e != null) { GetDbSet<CalendarEvent>().Remove(e); UnitOfWork.SaveChanges(); } }
    }
}
