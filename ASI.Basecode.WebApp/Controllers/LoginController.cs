using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.WebApp.Controllers
{
    public class LoginController : ControllerBase<LoginController>
    {
        private readonly SessionManager _sessionManager;
        private readonly SignInManager _signInManager;
        private readonly TokenValidationParametersFactory _tokenValidationParametersFactory;
        private readonly TokenProviderOptionsFactory _tokenProviderOptionsFactory;
        private readonly IConfiguration _appConfiguration;
        private readonly IUserService _userService;

        private const string AuthScheme = "ASI_Basecode";

        private static ClaimsPrincipal BuildPrincipal(User user)
        {
            var claims = new[]
            {
             new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
             new Claim(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
             new Claim(ClaimTypes.Surname, user.LastName ?? string.Empty),
             new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
             new Claim(ClaimTypes.Role, user.Role ?? string.Empty),
             new Claim("IdNumber", user.IdNumber ?? string.Empty),
             new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
    };

            var identity = new ClaimsIdentity(claims, AuthScheme);
            return new ClaimsPrincipal(identity);
        }

        private async Task SignInUserAsync(User user, bool rememberMe)
        {
            var principal = BuildPrincipal(user);

            await HttpContext.SignInAsync(AuthScheme, principal, new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                AllowRefresh = true
            });

            _session.SetString("IdNumber", user.IdNumber);
            _session.SetString("FullName", $"{user.FirstName} {user.LastName}");
            _session.SetString("Role", user.Role);
        }


        public LoginController(
            SignInManager signInManager,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IUserService userService,
            TokenValidationParametersFactory tokenValidationParametersFactory,
            TokenProviderOptionsFactory tokenProviderOptionsFactory
        ) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _sessionManager = new SessionManager(this._session);
            _signInManager = signInManager;
            _tokenProviderOptionsFactory = tokenProviderOptionsFactory;
            _tokenValidationParametersFactory = tokenValidationParametersFactory;
            _appConfiguration = configuration;
            _userService = userService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult StudentLogin()
        {
            return View("StudentLogin", new StudentLoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> StudentLogin(StudentLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/StudentLogin.cshtml", model);

            User user = null;
            var loginResult = _userService.AuthenticateUser(model.IdNumber, model.Password, ref user);

            if (loginResult == LoginResult.Inactive && user != null)
            {
                ViewData["ShowSuspendAccountModal"] = true;
                return View("~/Views/Login/StudentLogin.cshtml", model);
            }


            if (loginResult == LoginResult.Success && user != null && user.Role == "Student")
            {
                await SignInUserAsync(user, model.RememberMe);
                return RedirectToAction("Dashboard", "Student");
            }

            ModelState.AddModelError(string.Empty, "Incorrect ID Number or Password.");
            return View("~/Views/Login/StudentLogin.cshtml", model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TeacherLogin()
        {
            return View("TeacherLogin", new TeacherLoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> TeacherLogin(TeacherLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/TeacherLogin.cshtml", model);

            User user = null;
            var loginResult = _userService.AuthenticateUser(model.IdNumber, model.Password, ref user);

            if (loginResult == LoginResult.Inactive && user != null)
            {
                ViewData["ShowSuspendAccountModal"] = true;
                return View("~/Views/Login/TeacherLogin.cshtml", model);
            }

            if (loginResult == LoginResult.Success && user != null && user.Role == "Teacher")
            {
                await SignInUserAsync(user, model.RememberMe);
                return RedirectToAction("Dashboard", "Teacher");
            }

            ModelState.AddModelError(string.Empty, "Incorrect ID Number or Password.");
            return View("~/Views/Login/TeacherLogin.cshtml", model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AdminLogin()
        {
            return View("AdminLogin", new AdminLoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AdminLogin(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/AdminLogin.cshtml", model);

            User user = null;
            var loginResult = _userService.AuthenticateUser(model.IdNumber, model.Password, ref user);

            if (loginResult == LoginResult.Inactive && user != null)
            {
                ViewData["ShowSuspendAccountModal"] = true;
                return View("~/Views/Login/AdminLogin.cshtml", model);
            }

            if (loginResult == LoginResult.Success && user != null && user.Role == "Admin")
            {
                await SignInUserAsync(user, model.RememberMe);
                return RedirectToAction("Dashboard", "Admin");
            }

            ModelState.AddModelError(string.Empty, "Incorrect ID Number or Password.");
            return View("~/Views/Login/AdminLogin.cshtml", model);
        }

        [Authorize(Roles = "Student")]
        public IActionResult StudentDashboard()
        {
            return View();
        }

        [Authorize(Roles = "Teacher")]
        public IActionResult TeacherDashboard()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult AdminDashboard()
        {
            return View();
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentLogout()
        {
            await HttpContext.SignOutAsync("ASI_Basecode");
            return RedirectToAction("StudentLogin", "Login");
        }

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> TeacherLogout()
        {
            await HttpContext.SignOutAsync("ASI_Basecode");
            return RedirectToAction("TeacherLogin", "Login");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminLogout()
        {
            await HttpContext.SignOutAsync("ASI_Basecode");
            return RedirectToAction("AdminLogin", "Login");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPasswordSend(ResetPasswordRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var msg = ModelState.Values.SelectMany(v => v.Errors)
                                           .FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
                return Json(new { ok = false, message = msg });
            }

            string LinkFactory(string token) => Url.Action(
                "ResetPassword",
                "Login",
                new { token = Uri.EscapeDataString(token) },
                Request.Scheme
            );

            var result = await _userService.RequestPasswordResetAsync(
                model.Email,
                TimeSpan.FromMinutes(30),
                LinkFactory);

            return result switch
            {
                ForgotPasswordResult.Success => Json(new { ok = true, message = "If the email exists, a reset link has been sent." }),
                ForgotPasswordResult.NotFound => Json(new { ok = false, message = "Email must be associated with a registered account." }),
                ForgotPasswordResult.RateLimited => Json(new { ok = false, message = "Too many attempts. Try again later." }),
                _ => Json(new { ok = false, message = "An unexpected error occurred." })
            };
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token)
        {
            var user = _userService.ValidateResetToken(token);
            if (user == null) return View("ResetPasswordInvalid");
            return View("ResetPassword", new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View("ResetPassword", vm);

            var user = _userService.ValidateResetToken(vm.Token);
            if (user == null)
            {
                ModelState.AddModelError("", "This link is invalid or has expired.");
                return View("ResetPassword", vm);
            }

            if (vm.NewPassword != vm.ConfirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return View("ResetPassword", vm);
            }

            if (_userService.ResetPassword(user.UserId, vm.NewPassword))
            {
                _userService.MarkResetTokenUsed(vm.Token);
                TempData["SuccessMessage"] = "Password updated. You can now log in.";
                return RedirectToAction("StudentLogin", "Login");
            }

            ModelState.AddModelError("", "Could not update password. Try again.");
            return View("ResetPassword", vm);
        }
    }
}
