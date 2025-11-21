using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class ProgramCourse
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProgramCourseId { get; set; }

        [ForeignKey("Program")]
        public int ProgramId { get; set; }

        [ForeignKey("Course")]
        public int CourseId { get; set; }

        public int? Prerequisite { get; set; }  

        [ForeignKey("YearTerm")]
        public int YearTermId { get; set; }

        public Program Program { get; set; }
        public Course Course { get; set; }
        public YearTerm YearTerm { get; set; }

        [ForeignKey("Prerequisite")]
        public Course PrerequisiteCourse { get; set; }
    }
}
