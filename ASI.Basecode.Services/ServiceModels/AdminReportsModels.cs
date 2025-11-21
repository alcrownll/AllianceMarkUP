using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ReportsDashboardModel
    {
        public string SchoolYear { get; set; }
        public string TermKey { get; set; }
        public IList<string> AvailableSchoolYears { get; set; } = new List<string>();
        public IList<ReportTermOptionModel> TermOptions { get; set; } = new List<ReportTermOptionModel>();
        public ReportsOverallModel Overall { get; set; } = new ReportsOverallModel();
        public ReportsTeacherModel Teacher { get; set; } = new ReportsTeacherModel();
        public ReportsStudentModel Student { get; set; } = new ReportsStudentModel();
    }

    public class ReportsOverallModel
    {
        public ReportsOverallSummary Summary { get; set; } = new ReportsOverallSummary();
        public IList<TrendPointModel> EnrollmentTrend { get; set; } = new List<TrendPointModel>();
        public IList<ProgramLeaderboardItemModel> ProgramLeaderboard { get; set; } = new List<ProgramLeaderboardItemModel>();
        public DemographicBreakdownModel Demographics { get; set; } = new DemographicBreakdownModel();
        public CourseOutcomeModel CourseOutcomes { get; set; } = new CourseOutcomeModel();
        public IList<RiskIndicatorModel> RiskIndicators { get; set; } = new List<RiskIndicatorModel>();
        public CapacityLoadModel Capacity { get; set; } = new CapacityLoadModel();
    }

    public class ReportsOverallSummary
    {
        public int TotalEnrolled { get; set; }
        public decimal GrowthPercent { get; set; }
        public decimal AverageGwa { get; set; }
        public decimal PassRatePercent { get; set; }
        public decimal RetentionPercent { get; set; }
    }

    public class TrendPointModel
    {
        public string Label { get; set; }
        public string TermKey { get; set; }
        public int Value { get; set; }
    }

    public class ProgramLeaderboardItemModel
    {
        public string Program { get; set; }
        public int Enrollment { get; set; }
        public decimal GrowthPercent { get; set; }
    }

    public class DemographicBreakdownModel
    {
        public IList<NamedValueModel> GenderSplit { get; set; } = new List<NamedValueModel>();
        public IList<NamedValueModel> AgeBands { get; set; } = new List<NamedValueModel>();
        public IList<NamedValueModel> Statuses { get; set; } = new List<NamedValueModel>();
    }

    public class NamedValueModel
    {
        public string Name { get; set; }
        public decimal Value { get; set; }
    }

    public class CourseOutcomeModel
    {
        public IList<CourseStatModel> HighestFailureRates { get; set; } = new List<CourseStatModel>();
        public IList<CourseStatModel> BestPerforming { get; set; } = new List<CourseStatModel>();
    }

    public class CourseStatModel
    {
        public string CourseCode { get; set; }
        public decimal MetricValue { get; set; }
    }

    public class RiskIndicatorModel
    {
        public string Label { get; set; }
        public int Count { get; set; }
    }

    public class CapacityLoadModel
    {
        public int SectionsNearCapacity { get; set; }
        public int FacultyHighLoad { get; set; }
        public decimal AverageClassSize { get; set; }
    }

    public class ReportsTeacherModel
    {
        public IList<TeacherDirectoryItemModel> Directory { get; set; } = new List<TeacherDirectoryItemModel>();
        public TeacherDetailModel SelectedTeacher { get; set; } = new TeacherDetailModel();
        public TeacherDetailModel AggregateDetail { get; set; } = new TeacherDetailModel();
        public IList<ReportsCourseOptionModel> Courses { get; set; } = new List<ReportsCourseOptionModel>();
        public int? SelectedCourseId { get; set; }
    }

    public class TeacherDirectoryItemModel
    {
        public int TeacherId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Email { get; set; }
        public string Rank { get; set; }
        public decimal LoadUnits { get; set; }
        public int Sections { get; set; }
        public IList<int> ProgramIds { get; set; } = new List<int>();
        public IList<int> CourseIds { get; set; } = new List<int>();
    }

    public class TeacherDetailModel
    {
        public int TeacherId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Email { get; set; }
        public string Rank { get; set; }
        public decimal TeachingLoadUnits { get; set; }
        public int TeachingLoadCount { get; set; }
        public int SectionCount { get; set; }
        public decimal PassRatePercent { get; set; }
        public decimal SubmissionCompletionPercent { get; set; }
        public IList<TeacherAssignmentModel> Assignments { get; set; } = new List<TeacherAssignmentModel>();
        public IList<CoursePassRateModel> CoursePassRates { get; set; } = new List<CoursePassRateModel>();
        public IList<TeacherSubmissionStatusModel> SubmissionStatuses { get; set; } = new List<TeacherSubmissionStatusModel>();
        public IList<NamedValueModel> SubmissionSummary { get; set; } = new List<NamedValueModel>();
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public int IncompleteCount { get; set; }
        public bool IsAggregate { get; set; }
        public string ContextLabel { get; set; }
    }

    public class TeacherAssignmentModel
    {
        public string CourseCode { get; set; }
        public string SubjectName { get; set; }
        public string Section { get; set; }
        public string Schedule { get; set; }
        public decimal Units { get; set; }
        public int Enrolled { get; set; }
        public decimal? FinalGrade { get; set; }
        public string Status { get; set; }
    }

    public class CoursePassRateModel
    {
        public string CourseCode { get; set; }
        public string SubjectName { get; set; }
        public decimal PassRatePercent { get; set; }
    }

    public class TeacherSubmissionStatusModel
    {
        public string CourseCode { get; set; }
        public string SubjectName { get; set; }
        public string Status { get; set; }
        public bool IsComplete { get; set; }
    }

    public class ReportsProgramOptionModel
    {
        public int ProgramId { get; set; }
        public string ProgramCode { get; set; }
        public string ProgramName { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(ProgramCode)
            ? ProgramName ?? "Program"
            : string.IsNullOrWhiteSpace(ProgramName) ? ProgramCode : $"{ProgramCode} - {ProgramName}";
    }

    public class ReportsCourseOptionModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(CourseName)
            ? CourseCode ?? "Course"
            : string.IsNullOrWhiteSpace(CourseCode) ? CourseName : $"{CourseCode} - {CourseName}";
    }

    public class TeacherOptionModel
    {
        public int TeacherId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
    }
    public class ReportsStudentModel
    {
        public IList<StudentOptionModel> Students { get; set; } = new List<StudentOptionModel>();
        public int? SelectedStudentId { get; set; }
        public StudentAnalyticsModel Analytics { get; set; } = new StudentAnalyticsModel();
        public StudentAnalyticsModel AggregateAnalytics { get; set; } = new StudentAnalyticsModel();
        public IList<ReportsProgramOptionModel> Programs { get; set; } = new List<ReportsProgramOptionModel>();
        public int? SelectedProgramId { get; set; }
        public IList<string> Sections { get; set; } = new List<string>();
        public IList<StudentCourseOptionModel> Courses { get; set; } = new List<StudentCourseOptionModel>();
        public int? SelectedCourseId { get; set; }
    }

    public class StudentOptionModel
    {
        public int StudentId { get; set; }
        public string Name { get; set; }
        public int? ProgramId { get; set; }
        public string Program { get; set; }
        public IList<string> Sections { get; set; } = new List<string>();
    }

    public class StudentCourseOptionModel
    {
        public int AssignedCourseId { get; set; }
        public int CourseId { get; set; }
        public int? ProgramId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public string SchoolYear { get; set; }
        public string TermKey { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(CourseName)
            ? CourseCode
            : string.IsNullOrWhiteSpace(CourseCode) ? CourseName : $"{CourseCode} - {CourseName}";
    }

    public class StudentAnalyticsModel
    {
        public IList<StudentTrendPointModel> GwaTrend { get; set; } = new List<StudentTrendPointModel>();
        public IList<StudentCourseGradeModel> CourseGrades { get; set; } = new List<StudentCourseGradeModel>();
        public StudentUnitsProgressModel UnitsProgress { get; set; } = new StudentUnitsProgressModel();
        public IList<NamedValueModel> StatusMix { get; set; } = new List<NamedValueModel>();
        public IList<string> Strengths { get; set; } = new List<string>();
        public IList<string> Risks { get; set; } = new List<string>();
        public StudentEngagementModel Engagement { get; set; } = new StudentEngagementModel();
        public IList<StudentSnapshotRowModel> Snapshot { get; set; } = new List<StudentSnapshotRowModel>();
        public IList<StudentGradeBreakdownModel> GradeBreakdown { get; set; } = new List<StudentGradeBreakdownModel>();
        public string ConsistencyLabel { get; set; }
        public IList<StudentComparativeHighlightModel> ComparativeHighlights { get; set; } = new List<StudentComparativeHighlightModel>();
        public bool IsAggregate { get; set; }
        public string ContextLabel { get; set; }
    }

    public class StudentComparativeHighlightModel
    {
        public string Course { get; set; }
        public decimal? Grade { get; set; }
        public string PeriodLabel { get; set; }
    }

    public class StudentTrendPointModel
    {
        public string TermKey { get; set; }
        public string Label { get; set; }
        public decimal Gwa { get; set; }
    }

    public class StudentCourseGradeModel
    {
        public string CourseCode { get; set; }
        public decimal Grade { get; set; }
    }

    public class StudentSnapshotRowModel
    {
        public string EdpCode { get; set; }
        public string IdNumber { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Program { get; set; }
        public string Gender { get; set; }
        public string YearLevel { get; set; }
        public decimal? Gwa { get; set; }
        public string Status { get; set; }
    }

    public class StudentGradeBreakdownModel
    {
        public string EdpCode { get; set; }
        public string Subject { get; set; }
        public decimal? Prelim { get; set; }
        public decimal? Midterm { get; set; }
        public decimal? Prefinal { get; set; }
        public decimal? Final { get; set; }
        public decimal? FinalGrade { get; set; }
        public string Status { get; set; }
    }

    public class StudentUnitsProgressModel
    {
        public int EarnedUnits { get; set; }
        public int RequiredUnits { get; set; }
    }

    public class StudentEngagementModel
    {
        public decimal AttendancePercent { get; set; }
        public decimal OnTimeSubmissionPercent { get; set; }
        public int MissingWorkCount { get; set; }
    }

    public class ReportTermOptionModel
    {
        public string TermKey { get; set; }
        public string Label { get; set; }
        public string SchoolYear { get; set; }
        public int SemesterOrder { get; set; }
    }
}
