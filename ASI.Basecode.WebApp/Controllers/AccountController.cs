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
using System;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AccountController : ControllerBase<AccountController>
    {
        private readonly SessionManager _sessionManager;
        private readonly SignInManager _signInManager;
        private readonly TokenValidationParametersFactory _tokenValidationParametersFactory;
        private readonly TokenProviderOptionsFactory _tokenProviderOptionsFactory;
        private readonly IConfiguration _appConfiguration;
        private readonly IUserService _userService;
        private readonly IManageAccountsService _manageAccountsService;

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

            // Keep your existing session values if other parts of the app still read them
            _session.SetString("IdNumber", user.IdNumber);
            _session.SetString("FullName", $"{user.FirstName} {user.LastName}");
            _session.SetString("Role", user.Role);
        }


        public AccountController(
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

        // =======================
        // STUDENT LOGIN
        // =======================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult StudentLogin()
        {
            return View("StudentLogin", new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> StudentLogin(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/StudentLogin.cshtml", model);

            User user = null;
            var loginResult = _userService.AuthenticateUser(model.IdNumber, model.Password, ref user);

            if (loginResult == LoginResult.Success && user != null && user.Role == "Student")
            {
                await SignInUserAsync(user, model.RememberMe);
                return RedirectToAction("Dashboard", "Student");
            }

            TempData["ErrorMessage"] = "Invalid ID Number or Password.";
            return View("~/Views/Login/StudentLogin.cshtml", model);
        }

        // =======================
        // TEACHER LOGIN
        // =======================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult TeacherLogin()
        {
            return View("TeacherLogin", new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> TeacherLogin(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/TeacherLogin.cshtml", model);

            User user = null;
            var loginResult = _userService.AuthenticateUser(model.IdNumber, model.Password, ref user);

            if (loginResult == LoginResult.Success && user != null && user.Role == "Teacher")
            {
                await SignInUserAsync(user, model.RememberMe);
                return RedirectToAction("TeacherDashboard", "Teacher");
            }

            TempData["ErrorMessage"] = "Invalid ID Number or Password.";
            return View("~/Views/Login/TeacherLogin.cshtml", model);
        }


        // =======================
        // ADMIN LOGIN
        // =======================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AdminLogin()
        {
            return View("AdminLogin", new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AdminLogin(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/AdminLogin.cshtml", model);

            User user = null;
            var loginResult = _userService.AuthenticateUser(model.IdNumber, model.Password, ref user);

            if (loginResult == LoginResult.Success && user != null && user.Role == "Admin")
            {
                await SignInUserAsync(user, model.RememberMe);
                return RedirectToAction("AdminDashboard", "Admin");
            }

            TempData["ErrorMessage"] = "Invalid ID Number or Password.";
            return View("~/Views/Login/AdminLogin.cshtml", model);
        }


        // =======================
        // DASHBOARDS
        // =======================
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

        // =======================
        // LOGOUTS
        // =======================
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
    }
}
