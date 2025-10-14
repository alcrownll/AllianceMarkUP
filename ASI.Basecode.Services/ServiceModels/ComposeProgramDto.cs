using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ComposeProgramDto
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Notes { get; set; } = "";
        public List<ComposeYearDto> Years { get; set; } = new();
    }

    public class ComposeYearDto
    {
        public int Year { get; set; }
        public List<ComposeTermDto> Terms { get; set; } = new();
    }

    public class ComposeTermDto
    {
        public int Term { get; set; }
        public List<ComposeCourseDto> Courses { get; set; } = new();
    }

    public class ComposeCourseDto
    {
        public int CourseId { get; set; }
        public int PrereqCourseId { get; set; }
    }
}
