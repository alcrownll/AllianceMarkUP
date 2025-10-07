namespace ASI.Basecode.Services.ServiceModels
{
    public class DashboardSummaryModel
    {
        public int TotalStudents { get; set; }
        public decimal StudentsChangePercent { get; set; }
        public int TotalTeachers { get; set; }
        public decimal TeachersChangePercent { get; set; }
        public int ActiveCourses { get; set; }
        public decimal ActiveCoursesChangePercent { get; set; }
    }
}
