using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;

        public AdminController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
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
        public IActionResult Reports()
        {
            return View("AdminReports");
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
