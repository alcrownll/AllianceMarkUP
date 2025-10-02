using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class ManageAccountsController : Controller
    {
        private readonly IManageAccountsService _svc;

        public ManageAccountsController(IManageAccountsService svc)
        {
            _svc = svc;
        }

        // TABS
        [HttpGet]
        public async Task<IActionResult> Index(
            string? tab, string? program, string? yearLevel, string? name, string? idNumber, CancellationToken ct)
        {
            var isTeachers = (tab?.ToLower() == "teachers");
            var filters = new ManageAccountsFilters
            {
                Program = program,
                YearLevel = yearLevel,
                Name = name,
                IdNumber = idNumber
            };

            ManageAccountsViewModel vm;

            if (isTeachers)
            {
                var result = await _svc.GetTeachersAsync(filters, ct);
                vm = new ManageAccountsViewModel
                {
                    ActiveTab = ManageTab.Teachers,
                    Filters = result.Filters,
                    Teachers = result.Teachers
                };
            }
            else
            {
                var result = await _svc.GetStudentsAsync(filters, ct);
                vm = new ManageAccountsViewModel
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

        // STUDENTS: TEMPLATE + IMPORT 

        [HttpGet]
        public IActionResult DownloadStudentTemplate()
        {
            var (bytes, contentType, fileName) = _svc.GenerateStudentsTemplate();
            return File(bytes, contentType, fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUploadStudents(
            IFormFile file, string? defaultAccountStatus, string? defaultStudentStatus, CancellationToken ct)
        {
            var result = await _svc.ImportStudentsAsync(
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

        // TEACHERS: TEMPLATE + IMPORT 

        [HttpGet]
        public IActionResult DownloadTeacherTemplate()
        {
            var (bytes, contentType, fileName) = _svc.GenerateTeachersTemplate();
            return File(bytes, contentType, fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUploadTeachers(
            IFormFile file, string? defaultAccountStatus, string? defaultPosition, CancellationToken ct)
        {
            var result = await _svc.ImportTeachersAsync(
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
