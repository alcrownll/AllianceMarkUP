using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AdminAccountsController : Controller
    {
        private readonly IAdminAccountsService _adminacc;
        private readonly IAdminCreateAccountService _create;

        public AdminAccountsController(
            IAdminAccountsService adminacc,
            IAdminCreateAccountService create)
        {
            _adminacc = adminacc;
            _create = create;
        }

        // TABS 
        [HttpGet]
        public async Task<IActionResult> Index(
            string? tab, string? program, string? yearLevel, string? name, string? idNumber, CancellationToken ct)
        {
            var isTeachers = (tab?.ToLower() == "teachers");
            var filters = new AccountsFilters
            {
                Program = program,
                YearLevel = yearLevel,
                Name = name,
                IdNumber = idNumber
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
