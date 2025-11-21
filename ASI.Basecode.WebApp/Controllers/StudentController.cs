using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IClassScheduleRepository _classScheduleRepository;
        private readonly IWebHostEnvironment _env;
        private readonly IProfileService _profileService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly INotificationService _notificationService;
        private readonly IStudentDashboardService _studentDashboardService;
        private readonly IStudyLoadService _studyLoadService;
        private readonly IStudentGradesService _studentGradesService;

        public StudentController(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            IUserRepository userRepository,
            IClassScheduleRepository classScheduleRepository,
            IWebHostEnvironment env,
            IProfileService profileService,
            IHttpContextAccessor httpContext,
            INotificationService notificationService,
            IStudentDashboardService studentDashboardService,
            IStudyLoadService studyLoadService,
            IStudentGradesService studentGradesService
            )
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _userRepository = userRepository;
            _classScheduleRepository = classScheduleRepository;
            _env = env;
            _profileService = profileService;
            _httpContext = httpContext;
            _notificationService = notificationService;
            _studentDashboardService = studentDashboardService;
            _studyLoadService = studyLoadService;
            _studentGradesService = studentGradesService;
        }

        // --------------------------------------------------------------------
        // DASHBOARD
        // --------------------------------------------------------------------
        public async Task<IActionResult> Dashboard()
        {
            ViewData["PageHeader"] = "Dashboard";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var vm = await _studentDashboardService.BuildAsync(idNumber);
            return View("StudentDashboard", vm);
        }

        // --------------------------------------------------------------------
        // PROFILE
        // --------------------------------------------------------------------
        public async Task<IActionResult> Profile(CancellationToken ct)
        {
            ViewData["PageHeader"] = "Profile";

            int userId = _profileService.GetCurrentUserId();
            var vm = await _profileService.GetStudentProfileAsync(userId);
            if (vm == null) return NotFound();

            ViewBag.Programs = await _profileService.GetActiveProgramsAsync(ct);

            return View("StudentProfile", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveProfile(StudentProfileViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Programs = await _profileService.GetActiveProgramsAsync(ct);
                return View("StudentProfile", vm);
            }

            int userId = _profileService.GetCurrentUserId();
            await _profileService.UpdateStudentProfileAsync(userId, vm);

            TempData["ProfileSaved"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> StudyLoad(string term = null)
        {
            ViewData["PageHeader"] = "Study Load";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber);

            if (user == null)
                return View(new StudyLoadViewModel());

            var vm = await _studyLoadService.GetStudyLoadAsync(user.UserId, term);
            return View(vm); // View name is StudyLoad.cshtml
        }

        // --------------------------------------------------------------------
        // GRADES
        // --------------------------------------------------------------------
        public async Task<IActionResult> Grades(string schoolYear = null, string semester = null, CancellationToken ct = default)
        {
            ViewData["PageHeader"] = "Grades";

            var idNumber = HttpContext.Session.GetString("IdNumber");
            if (string.IsNullOrEmpty(idNumber))
                return RedirectToAction("StudentLogin", "Account");

            var user = await _userRepository.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdNumber == idNumber, ct);

            if (user == null)
                return View(new StudentGradesViewModel { StudentName = "Student", SchoolYear = GetCurrentSchoolYear() });

            var vm = await _studentGradesService.BuildAsync(user.UserId, schoolYear, semester, ct);
            return View(vm);
        }

        // --------------------------------------------------------------------
        // CALENDAR & NOTIFICATIONS
        // --------------------------------------------------------------------
        public IActionResult Calendar() => RedirectToAction("Index", "Calendar");

        public IActionResult Notifications() => RedirectToAction("Index", "Notifications");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkNotificationRead(int id)
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkRead(userId, id);
            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllNotificationsRead()
        {
            var userId = _profileService.GetCurrentUserId();
            _notificationService.MarkAllRead(userId);
            return RedirectToAction(nameof(Notifications));
        }

        // --------------------------------------------------------------------
        // PROSPECTUS DOWNLOAD (BSCS / BSIT)
        // --------------------------------------------------------------------
        [HttpGet("/Student/DownloadProspectus")]
        public async Task<IActionResult> DownloadProspectus([FromQuery] string program = null)
        {
            // If a program is not provided (normal case), resolve from logged-in student
            if (string.IsNullOrWhiteSpace(program))
            {
                var idNumber = HttpContext.Session.GetString("IdNumber");
                if (string.IsNullOrEmpty(idNumber))
                    return RedirectToAction("StudentLogin", "Account");

                var user = await _userRepository.GetUsers()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.IdNumber == idNumber);
                if (user == null) return NotFound("User not found.");

                var student = await _studentRepository.GetStudents()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == user.UserId);
                if (student == null) return NotFound("Student profile not found.");

                program = student.Program;
            }

            var prog = (program ?? string.Empty).Trim().ToUpperInvariant();

            // Accept strict codes or common variants
            string fileName = prog switch
            {
                "BSCS" => "BSCS.pdf",
                "BSIT" => "BSIT.pdf",
                _ when prog.Contains("IT") => "BSIT.pdf",
                _ when prog.Contains("CS") => "BSCS.pdf",
                _ => null
            };

            if (fileName is null)
                return NotFound("Program not mapped to a prospectus (expected BSCS or BSIT).");

            // Files expected at: wwwroot/prospectus/BSCS.pdf and BSIT.pdf
            var path = Path.Combine(_env.WebRootPath, "prospectus", fileName);
            if (!System.IO.File.Exists(path))
                return NotFound($"Prospectus file not found: {fileName}");

            // Force a download (use inline by omitting the downloadName)
            return PhysicalFile(path, "application/pdf", fileName);
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------
        private static string GetCurrentSchoolYear()
        {
            var now = DateTime.Now;
            var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }

    }
}
