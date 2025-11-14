using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminAccountsController : Controller
    {
        private readonly IAdminAccountsService _adminacc;
        private readonly IAdminCreateAccountService _create;
        private readonly IProfileService _profile;

        public AdminAccountsController(
            IAdminAccountsService adminacc,
            IAdminCreateAccountService create,
            IProfileService profileService)
        {
            _adminacc = adminacc;
            _create = create;
            _profile = profileService;
        }

        // TABS 
        [HttpGet]
        public async Task<IActionResult> Index(
            string? tab,
            string? program,
            string? yearLevel,
            string? name,
            string? idNumber,
            string? status,
            CancellationToken ct)
        {
            var normalizedStatus = string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase)
                ? "Inactive"
                : "Active";

            var isTeachers = (tab?.ToLower() == "teachers");
            var filters = new AccountsFilters
            {
                Program = program,
                YearLevel = yearLevel,
                Name = name,
                IdNumber = idNumber,
                Status = normalizedStatus
            };

            AdminAccountsViewModel vm;

            if (isTeachers)
            {
                var result = await _adminacc.GetTeachersAsync(filters, ct);
                vm = new AdminAccountsViewModel
                {
                    ActiveTab = ManageTab.Teachers,
                    Filters = result.Filters,
                    Teachers = result.Teachers
                };
            }
            else
            {
                var result = await _adminacc.GetStudentsAsync(filters, ct);
                vm = new AdminAccountsViewModel
                {
                    ActiveTab = ManageTab.Students,
                    Filters = result.Filters,
                    Students = result.Students,
                    Programs = result.Programs,
                    YearLevels = result.YearLevels
                };
            }

            return View("~/Views/Admin/AdminAccounts.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Student(int userId)
        {
            var vm = await _profile.GetStudentProfileAsync(userId);
            if (vm == null) return NotFound();
            ViewData["PageHeader"] = "Student Profile";

            return View("~/Views/Admin/AdminStudentProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStudent(int userId, StudentProfileViewModel vm)
        {
            // align route + hidden if needed
            if (userId <= 0 && vm.UserId > 0)
                userId = vm.UserId;

            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Student Profile";
                return View("~/Views/Admin/AdminStudentProfile.cshtml", vm);
            }

            var adminUserId = _profile.GetCurrentUserId();

            await _profile.UpdateStudentProfileByAdminAsync(
                adminUserId: adminUserId,
                targetUserId: userId,
                input: vm);

            TempData["ProfileSaved"] = "Profile has been updated.";
            return RedirectToAction(nameof(Student), new { userId });
        }

        [HttpGet]
        public async Task<IActionResult> Teacher(int userId)
        {
            var vm = await _profile.GetTeacherProfileAsync(userId);
            if (vm == null) return NotFound();
            ViewData["PageHeader"] = "Teacher Profile";

            return View("~/Views/Admin/AdminTeacherProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTeacher(int userId, TeacherProfileViewModel vm)
        {
            if (userId <= 0 && vm.UserId > 0)
                userId = vm.UserId;

            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Teacher Profile";
                return View("~/Views/Admin/AdminTeacherProfile.cshtml", vm);
            }

            var adminUserId = _profile.GetCurrentUserId();

            await _profile.UpdateTeacherProfileByAdminAsync(
                adminUserId: adminUserId,
                targetUserId: userId,
                input: vm);

            TempData["ProfileSaved"] = "Profile has been updated.";
            return RedirectToAction(nameof(Teacher), new { userId });
        }

        /// <summary>
        /// Changes account status (suspend or reactivate) and logs a My Activity notification.
        /// </summary>
        /// <param name="userId">The user being changed.</param>
        /// <param name="tab">students/teachers (for redirect + wording).</param>
        /// <param name="status">
        /// New status for the account ("Inactive" to suspend, "Active" to reactivate).
        /// </param>
        /// <param name="viewStatus">
        /// Which list to show after redirect ("active" or "inactive").
        /// </param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendUser(
            int userId,
            string? tab,
            string? status,
            string? viewStatus,
            CancellationToken ct)
        {
            var adminUserId = _profile.GetCurrentUserId();
            var roleLabel = (tab?.ToLower() == "teachers") ? "teacher" : "student";

            // Default to "Inactive" if nothing is posted (shouldn't happen with our JS)
            var targetStatus = string.IsNullOrWhiteSpace(status) ? "Inactive" : status.Trim();

            var ok = await _adminacc.SuspendAccount(
                adminUserId: adminUserId,
                userId: userId,
                status: targetStatus,
                roleLabel: roleLabel,
                ct: ct);

            if (targetStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                TempData[ok ? "ImportOk" : "ImportErr"] = ok
                    ? "Account has been reactivated."
                    : "User not found or already active.";
            }
            else
            {
                TempData[ok ? "ImportOk" : "ImportErr"] = ok
                    ? "Account has been suspended."
                    : "User not found or already inactive.";
            }

            var normalizedTab = string.IsNullOrWhiteSpace(tab) ? "students" : tab;
            var normalizedStatus = string.IsNullOrWhiteSpace(viewStatus) ? "active" : viewStatus;

            return RedirectToAction(nameof(Index), new { tab = normalizedTab, status = normalizedStatus });
        }

        [HttpGet]
        public IActionResult CreateStudent()
        {
            var vm = new StudentProfileViewModel();
            ViewData["PageHeader"] = "Create Student";
            ViewData["IsCreate"] = true;
            ViewData["PostController"] = "AdminAccounts";
            ViewData["PostAction"] = "CreateStudent";
            return View("~/Views/Admin/AdminStudentProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudent(StudentProfileViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Create Student";
                ViewData["IsCreate"] = true;
                ViewData["PostController"] = "AdminAccounts";
                ViewData["PostAction"] = "CreateStudent";
                return View("~/Views/Admin/AdminStudentProfile.cshtml", vm);
            }

            var defaults = new ImportUserDefaults
            {
                DefaultAccountStatus = "Active",
                DefaultStudentStatus = "Enrolled"
            };

            var adminUserId = _profile.GetCurrentUserId();

            var (userId, idNumber) = await _create.CreateSingleStudentAsync(
                adminUserId: adminUserId,
                vm: vm,
                defaults: defaults,
                ct: ct);

            TempData["ImportOk"] = $"Student created successfully. ID Number: {idNumber}";
            return RedirectToAction(nameof(Index), new { tab = "students" });
        }

        [HttpGet]
        public IActionResult CreateTeacher()
        {
            var vm = new TeacherProfileViewModel();
            ViewData["PageHeader"] = "Create Teacher";
            ViewData["IsCreate"] = true;
            ViewData["PostController"] = "AdminAccounts";
            ViewData["PostAction"] = "CreateTeacher";
            return View("~/Views/Admin/AdminTeacherProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacher(TeacherProfileViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Create Teacher";
                ViewData["IsCreate"] = true;
                ViewData["PostController"] = "AdminAccounts";
                ViewData["PostAction"] = "CreateTeacher";
                return View("~/Views/Admin/AdminTeacherProfile.cshtml", vm);
            }

            var defaults = new ImportUserDefaults
            {
                DefaultAccountStatus = "Active",
                DefaultTeacherPosition = "Instructor"
            };

            var adminUserId = _profile.GetCurrentUserId();

            var (userId, idNumber) = await _create.CreateSingleTeacherAsync(
                adminUserId: adminUserId,
                vm: vm,
                defaults: defaults,
                ct: ct);

            TempData["ImportOk"] = $"Teacher created successfully. ID Number: {idNumber}";
            return RedirectToAction(nameof(Index), new { tab = "teachers" });
        }

        // excel:
        [HttpGet]
        public IActionResult DownloadStudentTemplate()
        {
            var (bytes, contentType, fileName) = _create.GenerateStudentsTemplate();
            return File(bytes, contentType, fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUploadStudents(
            IFormFile file,
            string? defaultAccountStatus,
            string? defaultStudentStatus,
            string? status,
            CancellationToken ct)
        {
            var defaults = new ImportUserDefaults
            {
                DefaultAccountStatus = string.IsNullOrWhiteSpace(defaultAccountStatus) ? "Active" : defaultAccountStatus!,
                DefaultStudentStatus = string.IsNullOrWhiteSpace(defaultStudentStatus) ? "Enrolled" : defaultStudentStatus!
            };

            var adminUserId = _profile.GetCurrentUserId();

            var result = await _create.ImportStudentsAsync(
                adminUserId: adminUserId,
                file: file,
                defaults: defaults,
                ct: ct);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = result.IsSuccess,
                    message = result.GetMessage()
                });
            }

            if (result.IsSuccess)
            {
                TempData["BulkUploadSuccess"] = result.GetMessage();
                return RedirectToAction(nameof(Index), new { tab = "students", status = status ?? "active" });
            }
            TempData["BulkUploadStudentsError"] = result.GetMessage();
            TempData["KeepStudentsModalOpen"] = "true";
            return RedirectToAction(nameof(Index), new { tab = "students", status = status ?? "active" });
        }

        // excel (teachers):
        [HttpGet]
        public IActionResult DownloadTeacherTemplate()
        {
            var (bytes, contentType, fileName) = _create.GenerateTeachersTemplate();
            return File(bytes, contentType, fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUploadTeachers(
            IFormFile file,
            string? defaultAccountStatus,
            string? defaultTeacherPosition,
            string? status,
            CancellationToken ct)
        {
            var defaults = new ImportUserDefaults
            {
                DefaultAccountStatus = string.IsNullOrWhiteSpace(defaultAccountStatus) ? "Active" : defaultAccountStatus!,
                DefaultTeacherPosition = string.IsNullOrWhiteSpace(defaultTeacherPosition) ? "Instructor" : defaultTeacherPosition!
            };

            var adminUserId = _profile.GetCurrentUserId();

            var result = await _create.ImportTeachersAsync(
                adminUserId: adminUserId,
                file: file,
                defaults: defaults,
                ct: ct);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = result.IsSuccess,
                    message = result.GetMessage()
                });
            }

            if (result.IsSuccess)
            {
                TempData["BulkUploadSuccess"] = result.GetMessage();
                return RedirectToAction(nameof(Index), new { tab = "teachers", status = status ?? "active" });
            }
            TempData["BulkUploadTeachersError"] = result.GetMessage();
            TempData["KeepTeachersModalOpen"] = "true";
            return RedirectToAction(nameof(Index), new { tab = "teachers", status = status ?? "active" });
        }
    }
}
