using System.Collections.Generic;
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

        public AdminController(IAdminDashboardService dashboardService, IAdminReportsService reportsService)
        {
            _dashboardService = dashboardService;
            _reportsService = reportsService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> AdminDashboard()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "Test Admin"),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, "ASI_Basecode");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("ASI_Basecode", principal);

            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Dashboard(string schoolYear = null, string termKey = null)
        {
            var summary = await _dashboardService.GetSummaryAsync();
            var trend = await _dashboardService.GetEnrollmentTrendAsync();
            var schoolYears = await _dashboardService.GetAvailableSchoolYearsAsync();
            var detail = await _dashboardService.GetYearDetailAsync(schoolYear, termKey);

            var vm = new AdminDashboardViewModel
            {
                Summary = summary,
                EnrollmentTrend = trend,
                SchoolYears = schoolYears,
                SelectedSchoolYear = detail?.SchoolYear,
                YearDetail = detail
            };

            return View("AdminDashboard", vm);
        }

        // ✅ Manage Accounts (Students, Teachers)
        [Authorize(Roles = "Admin")]
        public IActionResult Accounts()
        {
            return View("AdminAccounts");
        }

        // ✅ Manage Courses (catalog of subjects)
        [Authorize(Roles = "Admin")]
        public IActionResult Courses()
        {
            return View("AdminCourses");
        }

        // ✅ Manage Classes (specific offerings, schedules, teachers, enrolled students)
        [Authorize(Roles = "Admin")]
        public IActionResult Classes()
        {
            return View("AdminClasses");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reports(string schoolYear = null, string termKey = null, int? teacherId = null, int? studentId = null)
        {
            var dashboard = await _reportsService.GetDashboardAsync(schoolYear, termKey, teacherId, studentId);

            var vm = new AdminReportsViewModel
            {
                Dashboard = dashboard,
                SchoolYears = dashboard?.AvailableSchoolYears ?? new List<string>(),
                SelectedSchoolYear = dashboard?.SchoolYear,
                SelectedTermKey = dashboard?.TermKey,
                SelectedTeacherId = teacherId,
                SelectedStudentId = studentId
            };

            return View("AdminReports", vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ReportsDashboard(string schoolYear = null, string termKey = null, int? teacherId = null, int? studentId = null)
        {
            var dashboard = await _reportsService.GetDashboardAsync(schoolYear, termKey, teacherId, studentId);
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


        // ✅ Calendar (school events, deadlines)
        [Authorize(Roles = "Admin")]
        public IActionResult Calendar()
        {
            ViewData["PageHeader"] = "Calendar";
            return View("~/Views/Shared/Partials/Calendar.cshtml");
        }

        // ✅ Notifications (send system-wide announcements)
        [Authorize(Roles = "Admin")]
        public IActionResult Notifications()
        {
            ViewData["PageHeader"] = "Student Notifications";
            return View("~/Views/Shared/Partials/Notifications.cshtml");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> DashboardYearDetail(string schoolYear, string termKey = null)
        {
            var detail = await _dashboardService.GetYearDetailAsync(schoolYear, termKey);
            if (detail == null)
            {
                return NotFound();
            }

            return Json(detail);
        }
    }
}
