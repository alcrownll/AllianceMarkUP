namespace ASI.Basecode.Services.ServiceModels
{
    public class EnrollmentTrendPointModel
    {
        public string TermKey { get; set; }
        public string Label { get; set; }
        public int YearStart { get; set; }
        public int SemesterOrder { get; set; }
        public int StudentCount { get; set; }
    }
}
