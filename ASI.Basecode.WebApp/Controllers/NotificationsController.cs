using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Student,Teacher,Admin")]
    public class NotificationsController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly INotificationService _notificationService;
        private readonly IRightSidebarService _rightSidebar;

        public NotificationsController(
            IProfileService profileService,
            INotificationService notificationService,
            IRightSidebarService rightSidebar)
        {
            _profileService = profileService;
            _notificationService = notificationService;
            _rightSidebar = rightSidebar;
        }

        private async Task SetRightSidebarAsync()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                ViewData["RightSidebar"] =
                    await _rightSidebar.BuildAsync(User, takeNotifications: 5, takeEvents: 5);
            }
        }

        [HttpGet("/Student/Notifications", Name = "StudentNotifications")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentIndex() => await BuildIndexViewAsync();

        [HttpGet("/Teacher/Notifications", Name = "TeacherNotifications")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> TeacherIndex() => await BuildIndexViewAsync();

        [HttpGet("/Admin/Notifications", Name = "AdminNotifications")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminIndex() => await BuildIndexViewAsync();

        [HttpGet("/Notifications")]
        public IActionResult Index() => RedirectToRoleNotifications();

        private async Task<IActionResult> BuildIndexViewAsync()
        {
            ViewData["PageHeader"] = "Notifications";
            await SetRightSidebarAsync();

            var userId = _profileService.GetCurrentUserId();
            var items = _notificationService.GetLatest(userId, page: 1, pageSize: 100);

            var vm = new NotificationPageVm
            {
                Items = items,
                UnreadCount = items.Count(x => !x.IsRead)
            };

            return View("~/Views/Shared/Partials/Notifications.cshtml", vm);
        }

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
        public IActionResult MarkAllRead(string scope)
        {
            var userId = _profileService.GetCurrentUserId();

            // scope: "all" | "updates" | "activity"
            if (string.Equals(scope, "updates", StringComparison.OrdinalIgnoreCase))
            {
                _notificationService.MarkAllRead(userId, NotificationKind.System);
            }
            else if (string.Equals(scope, "activity", StringComparison.OrdinalIgnoreCase))
            {
                _notificationService.MarkAllRead(userId, NotificationKind.Activity);
            }
            else
            {
                // default/all tab
                _notificationService.MarkAllRead(userId, kind: null);
            }

            return RedirectToRoleNotifications();
        }

        private IActionResult RedirectToRoleNotifications()
        {
            if (User.IsInRole("Admin")) return RedirectToRoute("AdminNotifications");
            if (User.IsInRole("Teacher")) return RedirectToRoute("TeacherNotifications");
            return RedirectToRoute("StudentNotifications");
        }
    }
}
