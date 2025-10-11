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

        public int YearLevel { get; set; }  // 1, 2, 3, 4
        public int Term { get; set; }       // 1 or 2

        // Optional navigation property (if you want EF to connect automatically)
        public ICollection<ProgramCourse> ProgramCourses { get; set; }
    }
}
