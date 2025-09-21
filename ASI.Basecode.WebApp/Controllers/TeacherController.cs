using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class TeacherController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TeacherDashboard()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "Test Teacher"),
                new Claim(ClaimTypes.Role, "Teacher")
            };

            var identity = new ClaimsIdentity(claims, "ASI_Basecode");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("ASI_Basecode", principal);

            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult Dashboard()
        {
            ViewData["PageHeader"] = "Teacher Dashboard";
            return View("TeacherDashboard");
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult Profile()
        {
            ViewData["PageHeader"] = "Teacher Profile";
            return View("~/Views/Shared/Partials/Profile.cshtml"); // Shared UI
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult AssignedCourses()
        {
            ViewData["PageHeader"] = "Assigned Courses";
            return View("TeacherCourses"); // explicitly point to TeacherCourses.cshtml
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult Calendar()
        {
            ViewData["PageHeader"] = "Calendar";
            return View("~/Views/Shared/Partials/Calendar.cshtml"); // Shared UI
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult Notifications()
        {
            ViewData["PageHeader"] = "Notifications";
            return View("~/Views/Shared/Partials/Notifications.cshtml"); // Shared UI
        }

        //Logout

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("ASI_Basecode");
            return RedirectToAction("TeacherLogin", "Login"); // redirect to login page
        }
    }
}
