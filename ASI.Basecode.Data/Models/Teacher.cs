using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class Teacher
    {
        [Key]
        public int TeacherId { get; set; } 
        public string Position { get; set; }
        public int UserId { get; set; }

        public User User { get; set; }
        public ICollection<AssignedCourse> AssignedCourses { get; set; } = new List<AssignedCourse>();
    }
}
