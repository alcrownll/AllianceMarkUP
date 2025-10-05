using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AdminController : Controller
    {

        [Authorize(Roles = "Admin")]
        public IActionResult Dashboard()
        {
            return View("AdminDashboard");
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
    }
}
