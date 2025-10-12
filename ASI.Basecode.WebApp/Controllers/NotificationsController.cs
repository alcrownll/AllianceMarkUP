using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Student,Teacher,Admin")]
    public class NotificationsController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly INotificationService _notificationService;

        public NotificationsController(IProfileService profileService,
                                       INotificationService notificationService)
        {
            _profileService = profileService;
            _notificationService = notificationService;
        }

        // --- Canonical GET routes per role (named) --------------------------
        [HttpGet("/Student/Notifications", Name = "StudentNotifications")]
        [Authorize(Roles = "Student")]
        public IActionResult StudentIndex() => BuildIndexView();

        [HttpGet("/Teacher/Notifications", Name = "TeacherNotifications")]
        [Authorize(Roles = "Teacher")]
        public IActionResult TeacherIndex() => BuildIndexView();

        [HttpGet("/Admin/Notifications", Name = "AdminNotifications")]
        [Authorize(Roles = "Admin")]
        public IActionResult AdminIndex() => BuildIndexView();

        // Shared builder used by all three
        private IActionResult BuildIndexView()
        {
            ViewData["PageHeader"] = "Notifications";

            var userId = _profileService.GetCurrentUserId();
            var items = _notificationService.GetLatest(userId, page: 1, pageSize: 100);

            var vm = new NotificationPageVm
            {
                Items = items,
                UnreadCount = items.Count(x => !x.IsRead)
            };

            return View("~/Views/Shared/Partials/Notifications.cshtml", vm);
        }

        // --- POST actions (role-agnostic) -----------------------------------

        [HttpPost("/Notifications/MarkRead")]
        [ValidateAntiForgeryToken]
        public IActionResult MarkRead(int id)
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkRead(userId, id);
            return RedirectToRoleNotifications();
        }

        [HttpPost("/Notifications/MarkAllRead")]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllRead()
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkAllRead(userId);
            return RedirectToRoleNotifications();
        }

        // Redirect helper → sends user back to proper role URL
        private IActionResult RedirectToRoleNotifications()
        {
            if (User.IsInRole("Admin")) return RedirectToRoute("AdminNotifications");
            if (User.IsInRole("Teacher")) return RedirectToRoute("TeacherNotifications");
            // default to Student if both/none (based on your auth model)
            return RedirectToRoute("StudentNotifications");
        }
    }
}
