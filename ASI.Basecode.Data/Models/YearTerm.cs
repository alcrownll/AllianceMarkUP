using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class YearTerm
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int YearTermId { get; set; }     

        public int YearLevel { get; set; }       // 1..4

        [Column("TermNumber")]
        public int TermNumber { get; set; }            // 1..2

        public ICollection<ProgramCourse> ProgramCourses { get; set; }
    }
}
