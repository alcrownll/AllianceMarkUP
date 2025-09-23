using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class ClassSchedule
    {
        [Key]
        [ForeignKey("AssignedCourse")]
        public int ClassScheduleId { get; set; }
        public int AssignedCourseId { get; set; }
        public DayOfWeek Day { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Room { get; set; }

        public AssignedCourse AssignedCourse { get; set; }
    }
}
