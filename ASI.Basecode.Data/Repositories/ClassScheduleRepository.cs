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
    public class ClassScheduleRepository : BaseRepository, IClassScheduleRepository
    {
        public ClassScheduleRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<ClassSchedule> GetClassSchedules() => this.GetDbSet<ClassSchedule>();
        public ClassSchedule GetClassScheduleById(int classScheduleId) => this.GetDbSet<ClassSchedule>().FirstOrDefault(cs => cs.ClassScheduleId == classScheduleId);

        public void AddClassSchedule(ClassSchedule schedule)
        {
            this.GetDbSet<ClassSchedule>().Add(schedule);
            UnitOfWork.SaveChanges();
        }

        public void UpdateClassSchedule(ClassSchedule schedule)
        {
            this.GetDbSet<ClassSchedule>().Update(schedule);
            UnitOfWork.SaveChanges();
        }

        public void DeleteClassSchedule(int classScheduleId)
        {
            var cs = GetClassScheduleById(classScheduleId);
            if (cs != null)
            {
                this.GetDbSet<ClassSchedule>().Remove(cs);
                UnitOfWork.SaveChanges();
            }
        }
    }
}