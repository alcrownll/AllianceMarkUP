using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class StudentController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> StudentDashboard()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "Test Student"),
                new Claim(ClaimTypes.Role, "Student")
            };

            var identity = new ClaimsIdentity(claims, "ASI_Basecode");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("ASI_Basecode", principal);

            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Student")]
        public IActionResult Dashboard()
        {
            ViewData["PageHeader"] = "Dashboard";
            return View("StudentDashboard");
        }

        [Authorize(Roles = "Student")]
        public IActionResult Profile()
        {
            ViewData["PageHeader"] = "Profile";
            return View("~/Views/Shared/Partials/Profile.cshtml");
        }

        [Authorize(Roles = "Student")]
        public IActionResult StudyLoad()
        {
            ViewData["PageHeader"] = "Study Load";
            return View();
        }

        [Authorize(Roles = "Student")]
        public IActionResult Grades()
        {
            ViewData["PageHeader"] = "Grades";
            return View();
        }

        [Authorize(Roles = "Student")]
        public IActionResult Calendar()
        {
            ViewData["PageHeader"] = "Calendar";
            return View("~/Views/Shared/Partials/Calendar.cshtml");
        }

        [Authorize(Roles = "Student")]
        public IActionResult Notifications()
        {
            ViewData["PageHeader"] = "Student Notifications";
            return View("~/Views/Shared/Partials/Notifications.cshtml");
        }

    }
}
