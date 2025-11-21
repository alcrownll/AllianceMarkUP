using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;
        private readonly IAdminReportsService _reportsService;
        private readonly IProfileService _profileService;

        public AdminController(IAdminDashboardService dashboardService, IAdminReportsService reportsService, IProfileService profileService)
        {
            _dashboardService = dashboardService;
            _reportsService = reportsService;
            _profileService = profileService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> AdminDashboard()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, "ASI_Basecode");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("ASI_Basecode", principal);
            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Dashboard(string schoolYear = null, string termKey = null, int? programId = null)
        {
            var programs = await _dashboardService.GetProgramOptionsAsync();
            var schoolYears = await _dashboardService.GetAvailableSchoolYearsAsync();
            var detail = await _dashboardService.GetYearDetailAsync(schoolYear, termKey, programId);
            var resolvedSchoolYear = detail?.SchoolYear ?? schoolYear;
            var resolvedTermKey = detail?.SelectedTermKey ?? termKey;
            var summary = await _dashboardService.GetSummaryAsync(resolvedSchoolYear, resolvedTermKey, programId);
            var trend = await _dashboardService.GetEnrollmentTrendAsync(programId: programId);

            var vm = new AdminDashboardViewModel
            {
                Summary = summary,
                EnrollmentTrend = trend,
                SchoolYears = schoolYears,
                SelectedSchoolYear = detail?.SchoolYear,
                YearDetail = detail,
                Programs = programs,
                SelectedProgramId = programId,
                TermOptions =  new List<ReportTermOptionModel>(),
                SelectedTermKey = detail?.SelectedTermKey
            };

            return View("AdminDashboard", vm);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Accounts()
        {
            return View("AdminAccounts");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Courses()
        {
            return View("AdminCourses");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Classes()
        {
            return View("AdminClasses");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reports(string schoolYear = null, string termKey = null, int? teacherId = null, int? studentId = null, int? studentProgramId = null, int? studentCourseId = null)
        {
            var dashboard = await _reportsService.GetDashboardAsync(schoolYear, termKey, teacherId, studentId, studentProgramId, studentCourseId);

            var vm = new AdminReportsViewModel
            {
                Dashboard = dashboard,
                SchoolYears = dashboard?.AvailableSchoolYears ?? new List<string>(),
                SelectedSchoolYear = dashboard?.SchoolYear,
                SelectedTermKey = dashboard?.TermKey,
                SelectedTeacherId = teacherId,
                SelectedStudentId = studentId,
                SelectedStudentProgramId = studentProgramId,
                SelectedStudentCourseId = studentCourseId
            };

            return View("AdminReports", vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ReportsDashboard(string schoolYear = null, string termKey = null, int? teacherId = null, int? studentId = null, int? studentProgramId = null, int? studentCourseId = null)
        {
            var dashboard = await _reportsService.GetDashboardAsync(schoolYear, termKey, teacherId, studentId, studentProgramId, studentCourseId);
            return Json(dashboard);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ReportsTeacherDetail(int teacherId, string schoolYear = null, string termKey = null)
        {
            var detail = await _reportsService.GetTeacherDetailAsync(teacherId, schoolYear, termKey);
            return Json(detail);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ReportsStudentAnalytics(int studentId, string schoolYear = null, string termKey = null)
        {
            var analytics = await _reportsService.GetStudentAnalyticsAsync(studentId, schoolYear, termKey);
            return Json(analytics);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ReportsStudentDirectory(string schoolYear = null, string termKey = null, int? programId = null, int? courseId = null)
        {
            var students = await _reportsService.GetStudentDirectoryAsync(schoolYear, termKey, programId, courseId);
            return Json(students);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Calendar()
        {
            return RedirectToAction("Index", "Calendar");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Notifications() => RedirectToAction("Index", "Notifications");

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Profile()
        {
            ViewData["PageHeader"] = "Profile";

            int userId = _profileService.GetCurrentUserId();
            var vm = await _profileService.GetAdminProfileAsync(userId);
            if (vm == null) return NotFound();

            return View("AdminProfile", vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Profile";
                return View("AdminProfile", model);
            }

            int userId = _profileService.GetCurrentUserId();

            await _profileService.UpdateAdminProfileAsync(userId, model);

            TempData["ProfileUpdatedSuccess"] = "Profile updated successfully.";

            var vm = await _profileService.GetAdminProfileAsync(userId);

            ViewData["PageHeader"] = "Profile";
            return View("AdminProfile", vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> DashboardYearDetail(string schoolYear, string termKey = null, int? programId = null)
        {
            var summary = await _dashboardService.GetSummaryAsync(schoolYear, termKey, programId);
            var trend = await _dashboardService.GetEnrollmentTrendAsync(programId: programId);
            var detail = await _dashboardService.GetYearDetailAsync(schoolYear, termKey, programId);
            if (detail == null)
            {
                return NotFound();
            }

            return Json(new
            {
                summary,
                trend,
                detail
            });
        }
        
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> DiagnoseDashboardData()
        {
            var detail = await _dashboardService.GetYearDetailAsync(null, null);

            return Json(new
            {
                message = "Dashboard Data Diagnostic",
                schoolYear = detail?.SchoolYear,
                subjectEnrollmentsCount = detail?.SubjectEnrollments?.Count ?? 0,
                subjectEnrollments = detail?.SubjectEnrollments?.Take(3),
                subjectAverageGpaCount = detail?.SubjectAverageGpa?.Count ?? 0,
                subjectAverageGpa = detail?.SubjectAverageGpa?.Take(3),
                passFailRatesCount = detail?.PassFailRates?.Count ?? 0,
                passFailRates = detail?.PassFailRates,
                termOptionsCount = detail?.TermOptions?.Count ?? 0,
                termOptions = detail?.TermOptions,
                overallPassRate = detail?.OverallPassRate,
            });
        }
    }
}
