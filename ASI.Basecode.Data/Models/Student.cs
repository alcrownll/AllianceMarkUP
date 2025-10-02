using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class Student
    {
        [Key]
        public int StudentId { get; set; }
        public string AdmissionType { get; set; }
        public string Program { get; set; }
        public string Department { get; set; }
        public string YearLevel { get; set; }
        public string StudentStatus { get; set; }
        public int UserId { get; set; }

        public User User { get; set; }
        public ICollection<Grade> Grades { get; set; } = new List<Grade>();
    }
}
