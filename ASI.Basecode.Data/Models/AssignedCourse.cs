using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class AssignedCourse
    {
        [Key]
        [ForeignKey("Course")]
        public int AssignedCourseId { get; set; }
        public string EDPCode { get; set; }
        public int CourseId { get; set; }
        public string Type { get; set; }
        public int Units { get; set; }
        public int ProgramId { get; set; }
        public int TeacherId { get; set; }
        public string Semester { get; set; }
        public string SchoolYear { get; set; }
        public string Status { get; set; }

        public Course Course { get; set; }
        public Teacher Teacher { get; set; }
        public Program Program { get; set; }

        public ICollection<ClassSchedule> ClassSchedules { get; set; }
        public ICollection<Grade> Grades { get; set; }
    }
}
