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
        public async Task<IActionResult> Index(string? tab, string? program, string? yearLevel, string? name, string? idNumber, string? status, CancellationToken ct)
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

        // View and Edit Profiles:
        [HttpGet]
        public async Task<IActionResult> Student(int userId)
        {
            var vm = await _profile.GetStudentProfileAsync(userId);
            if (vm == null) return NotFound();
            ViewData["PageHeader"] = "Student Profile";

            // FIXED: correct view path under /Views/Admin/
            return View("~/Views/Admin/AdminStudentProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStudent(int userId, StudentProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Student Profile";
                return View("~/Views/Admin/AdminStudentProfile.cshtml", vm);
            }

            await _profile.UpdateStudentProfileAsync(userId, vm);
            TempData["ProfileSaved"] = "Profile has been updated.";
            return RedirectToAction(nameof(Student), new { userId });
        }

        [HttpGet]
        public async Task<IActionResult> Teacher(int userId)
        {
            var vm = await _profile.GetTeacherProfileAsync(userId);
            if (vm == null) return NotFound();
            ViewData["PageHeader"] = "Teacher Profile";

            // FIXED: correct view path AND filename
            return View("~/Views/Admin/AdminTeacherProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTeacher(int userId, TeacherProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PageHeader"] = "Teacher Profile";
                return View("~/Views/Admin/AdminTeacherProfile.cshtml", vm);
            }

            await _profile.UpdateTeacherProfileAsync(userId, vm);
            TempData["ProfileSaved"] = "Profile has been updated.";
            return RedirectToAction(nameof(Teacher), new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendUser(int userId, string? tab, CancellationToken ct)
        {
            var ok = await _adminacc.SuspendAccount(userId, "Inactive", ct);

            TempData[ok ? "ImportOk" : "ImportErr"] = ok
                ? "Account has been suspended."
                : "User not found or already inactive.";

            // Go back to whichever tab the button came from
            return RedirectToAction(nameof(Index), new { tab = string.IsNullOrWhiteSpace(tab) ? "students" : tab });
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

            var (_, idNumber) = await _create.CreateSingleStudentAsync(vm, defaults, ct);

            TempData["ImportOk"] = $"Student created successfully. ID Number: {idNumber}";
            return RedirectToAction(nameof(Index), new { tab = "students" });
        }


        // ===== SINGLE CREATE (TEACHER) =====

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

            var (_, idNumber) = await _create.CreateSingleTeacherAsync(vm, defaults, ct);

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
            IFormFile file, string? defaultAccountStatus, string? defaultStudentStatus, CancellationToken ct)
        {
            var result = await _create.ImportStudentsAsync(
                file,
                new ImportUserDefaults
                {
                    DefaultAccountStatus = string.IsNullOrWhiteSpace(defaultAccountStatus) ? "Active" : defaultAccountStatus!,
                    DefaultStudentStatus = string.IsNullOrWhiteSpace(defaultStudentStatus) ? "Enrolled" : defaultStudentStatus!
                },
                ct);

            TempData[result.HasErrors ? "ImportErr" : "ImportOk"] =
                result.HasErrors
                ? $"{(result.InsertedCount > 0 ? $"Imported {result.InsertedCount}. " : "")}{result.FailedCount} row(s) failed. First error: {result.FirstError}"
                : $"Imported {result.InsertedCount} student(s) successfully.";

            return RedirectToAction("Index", new { tab = "students" });
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
            IFormFile file, string? defaultAccountStatus, string? defaultPosition, CancellationToken ct)
        {
            var result = await _create.ImportTeachersAsync(
                file,
                new ImportUserDefaults
                {
                    DefaultAccountStatus = string.IsNullOrWhiteSpace(defaultAccountStatus) ? "Active" : defaultAccountStatus!,
                    DefaultTeacherPosition = string.IsNullOrWhiteSpace(defaultPosition) ? "Instructor" : defaultPosition!
                },
                ct);

            TempData[result.HasErrors ? "ImportErr" : "ImportOk"] =
                result.HasErrors
                ? $"{(result.InsertedCount > 0 ? $"Imported {result.InsertedCount}. " : "")}{result.FailedCount} row(s) failed. First error: {result.FirstError}"
                : $"Imported {result.InsertedCount} teacher(s) successfully.";

            return RedirectToAction("Index", new { tab = "teachers" });
        }
    }
}
