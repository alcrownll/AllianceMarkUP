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
    }

    public class TeacherDetailModel
    {
        public int TeacherId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Email { get; set; }
        public string Rank { get; set; }
        public decimal TeachingLoadUnits { get; set; }
        public int SectionCount { get; set; }
        public decimal PassRatePercent { get; set; }
        public decimal SubmissionCompletionPercent { get; set; }
        public IList<TeacherAssignmentModel> Assignments { get; set; } = new List<TeacherAssignmentModel>();
        public IList<CoursePassRateModel> CoursePassRates { get; set; } = new List<CoursePassRateModel>();
        public IList<TeacherSubmissionStatusModel> SubmissionStatuses { get; set; } = new List<TeacherSubmissionStatusModel>();
    }

    public class TeacherAssignmentModel
    {
        public string CourseCode { get; set; }
        public string Section { get; set; }
        public string Schedule { get; set; }
        public decimal Units { get; set; }
        public int Enrolled { get; set; }
    }

    public class CoursePassRateModel
    {
        public string CourseCode { get; set; }
        public decimal PassRatePercent { get; set; }
    }

    public class TeacherSubmissionStatusModel
    {
        public string CourseCode { get; set; }
        public string Status { get; set; }
        public bool IsComplete { get; set; }
    }

    public class ReportsStudentModel
    {
        public IList<StudentOptionModel> Students { get; set; } = new List<StudentOptionModel>();
        public int? SelectedStudentId { get; set; }
        public StudentAnalyticsModel Analytics { get; set; } = new StudentAnalyticsModel();
    }

    public class StudentOptionModel
    {
        public int StudentId { get; set; }
        public string Name { get; set; }
        public string Program { get; set; }
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
    }
}
