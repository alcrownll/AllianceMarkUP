using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class Program
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProgramId { get; set; }

        [Required]
        [MaxLength(10)]
        public string ProgramCode { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProgramName { get; set; }

        public bool IsActive { get; set; }

        // Navigation property
        public ICollection<ProgramCourse> ProgramCourses { get; set; }
    }
}
