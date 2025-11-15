// ServiceModels/ComposeProgramDto.cs
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// Payload for creating a Program with its full curriculum (years/terms/courses).
    /// Year/Term are lookups via YearTerm table on the server side.
    /// </summary>
    public class ComposeProgramDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Curriculum grouped by year (e.g., 1..4). Mutability via List for server-side normalization.
        /// </summary>
        public List<ComposeYearDto> Years { get; set; } = new();
    }

    /// <summary>
    /// DTO for updating basic program info. Curriculum updates can reuse Compose* DTOs as needed.
    /// </summary>
    public class UpdateProgramDto
    {
        public int ProgramId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

       
        public List<ComposeYearDto> Years { get; set; }
    }

    public class ComposeYearDto
    {
        /// <summary>
        /// Academic year number (e.g., 1..4). Server will map (Year, Term) to YearTermId.
        /// </summary>
        public int Year { get; set; }

        public List<ComposeTermDto> Terms { get; set; } = new();
    }

    public class ComposeTermDto
    {
        /// <summary>
        /// Term number (e.g., 1 or 2). Server will map (Year, Term) to YearTermId.
        /// </summary>
        public int Term { get; set; }

        public List<ComposeCourseDto> Courses { get; set; } = new();
    }

    public class ComposeCourseDto
    {
        public int CourseId { get; set; }

        /// <summary>
        /// Null means "no prerequisite". Coalesce to 0 (or keep null) on persistence based on your schema.
        /// </summary>
        public int   PrereqCourseId { get; set; }
    }
}
