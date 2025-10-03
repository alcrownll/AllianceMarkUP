using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
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
        private readonly IProfileService _profileService;

        public TeacherController(IProfileService profileService)
        {
            _profileService = profileService;
        }

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
        public async Task<IActionResult> Profile()
        {
            ViewData["PageHeader"] = "Profile";
            var vm = await _profileService.GetTeacherProfileAsync();
            if (vm == null) return NotFound();
            return View("TeacherProfile", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> SaveProfile(TeacherProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("TeacherProfile", vm);
            }

            await _profileService.UpdateTeacherProfileAsync(vm);
            TempData["ProfileSaved"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
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
