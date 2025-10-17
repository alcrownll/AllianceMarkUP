using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IProfileService _profileService;

        public AccountController(IAccountService accountService, IProfileService profileService)
        {
            _accountService = accountService;
            _profileService = profileService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Account/ChangePasswordAjax")]
        public async Task<IActionResult> ChangePasswordAjax([FromForm] string OldPassword, [FromForm] string NewPassword, [FromForm] string ConfirmPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword))
                    return Json(new { ok = false, message = "Please fill out all fields." });

                if (!string.Equals(NewPassword, ConfirmPassword))
                    return Json(new { ok = false, message = "Passwords do not match." });

                var userId = _profileService.GetCurrentUserId();
                var result = await _accountService.ChangePasswordAsync(userId, OldPassword, NewPassword);
                return Json(new { ok = result.ok, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Server error: " + ex.Message });
            }
        }
    }
}
