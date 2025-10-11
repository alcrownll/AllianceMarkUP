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

        [HttpGet]
        public IActionResult Index()
        {
            // Page header per role (optional)
            if (User.IsInRole("Admin")) ViewData["PageHeader"] = "Notifications";
            else if (User.IsInRole("Teacher")) ViewData["PageHeader"] = "Notifications";
            else ViewData["PageHeader"] = "Notifications";

            var userId = _profileService.GetCurrentUserId();
            var items = _notificationService.GetLatest(userId, page: 1, pageSize: 100);

            var vm = new NotificationPageVm
            {
                Items = items,
                UnreadCount = items.Count(x => !x.IsRead)
            };

            return View("~/Views/Shared/Partials/Notifications.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkRead(int id)
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkRead(userId, id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllRead()
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkAllRead(userId);
            return RedirectToAction(nameof(Index));
        }
    }
}
