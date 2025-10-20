using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class DashboardYearDetailModel
    {
        public string SchoolYear { get; set; }
        public IList<ProgramShareModel> ProgramShares { get; set; } = new List<ProgramShareModel>();
        public IList<YearLevelSeriesPointModel> YearLevelSeries { get; set; } = new List<YearLevelSeriesPointModel>();
        public IList<GpaTrendPointModel> AverageGpa { get; set; } = new List<GpaTrendPointModel>();
        public IList<PassFailRateModel> PassFailRates { get; set; } = new List<PassFailRateModel>();
        public IList<TermOptionModel> TermOptions { get; set; } = new List<TermOptionModel>();
        public string SelectedTermKey { get; set; }
        public decimal OverallPassRate { get; set; }
        public IList<SubjectEnrollmentPointModel> SubjectEnrollments { get; set; } = new List<SubjectEnrollmentPointModel>();
        public IList<SubjectGpaPointModel> SubjectAverageGpa { get; set; } = new List<SubjectGpaPointModel>();
    }

    public class ProgramShareModel
    {
        public string Program { get; set; }
        public int StudentCount { get; set; }
        public decimal SharePercent { get; set; }
    }

    public class YearLevelSeriesPointModel
    {
        public string YearLevel { get; set; }
        public string Program { get; set; }
        public int Count { get; set; }
    }

    public class GpaTrendPointModel
    {
        public string TermKey { get; set; }
        public string Label { get; set; }
        public decimal AverageGpa { get; set; }
    }

    public class PassFailRateModel
    {
        public string Program { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public decimal PassRate { get; set; }
        public decimal FailRate { get; set; }
        public string TermKey { get; set; }
        public string TermLabel { get; set; }
    }

    public class TermOptionModel
    {
        public string TermKey { get; set; }
        public string Label { get; set; }
        public string SchoolYear { get; set; }
        public int YearStart { get; set; }
        public int SemesterOrder { get; set; }
    }

    public class SubjectEnrollmentPointModel
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int StudentCount { get; set; }
    }

    public class SubjectGpaPointModel
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public decimal AverageGpa { get; set; }
    }
}