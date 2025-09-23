using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IClassScheduleRepository
    {
        IQueryable<ClassSchedule> GetClassSchedules();
        ClassSchedule GetClassScheduleById(int classScheduleId);
        void AddClassSchedule(ClassSchedule schedule);
        void UpdateClassSchedule(ClassSchedule schedule);
        void DeleteClassSchedule(int classScheduleId);
    }
}