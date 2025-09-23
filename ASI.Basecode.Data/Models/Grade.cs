using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class Grade
    {
        [Key]
        public int GradeId { get; set; }
        public int StudentId { get; set; }       
        public int AssignedCourseId { get; set; }   

        public decimal? Prelims { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? SemiFinal { get; set; }
        public decimal? Final { get; set; }
        public string Remarks { get; set; }

        public Student Student { get; set; }
        public AssignedCourse AssignedCourse { get; set; }
    }
}
