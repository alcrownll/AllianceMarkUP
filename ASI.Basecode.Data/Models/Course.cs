using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class Course
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string Description { get; set; }
        public int LecUnits { get; set; }
        public int LabUnits { get; set; }
        public int TotalUnits { get; set; }

        public ICollection<AssignedCourse> AssignedCourses { get; set; }
    }
}
