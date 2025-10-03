using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager; 
using ASI.Basecode.Services.ServiceModels;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class ManageAccountsService : IManageAccountsService
    {
        private readonly IStudentRepository _students;
        private readonly ITeacherRepository _teachers;
        private readonly IUserRepository _users;
        private readonly IUserProfileRepository _profiles;
        private readonly IUnitOfWork _uow;

        public ManageAccountsService(
            IStudentRepository students,
            ITeacherRepository teachers,
            IUserRepository users,
            IUserProfileRepository profiles,
            IUnitOfWork uow)
        {
            _students = students;
            _teachers = teachers;
            _users = users;
            _profiles = profiles;
            _uow = uow;
        }

        // fetch data and filters

        public async Task<ManageAccountsResult> GetStudentsAsync(ManageAccountsFilters filters, CancellationToken ct)
        {
            var query = _students.GetStudentsWithUser();

            if (!string.IsNullOrWhiteSpace(filters.Program))
                query = query.Where(s => s.Program == filters.Program);

            if (!string.IsNullOrWhiteSpace(filters.YearLevel))
                query = query.Where(s => s.YearLevel == filters.YearLevel);

            if (!string.IsNullOrWhiteSpace(filters.Name))
            {
                var name = filters.Name.Trim().ToLower();
                query = query.Where(s => (s.User.FirstName + " " + s.User.LastName).ToLower().Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(filters.IdNumber))
            {
                var id = filters.IdNumber.Trim();
                query = query.Where(s => s.User.IdNumber.Contains(id));
            }

            var rows = await query
                .OrderBy(s => s.Program)
                .ThenBy(s => s.YearLevel)
                .ThenBy(s => s.User.LastName)
                .Select(s => new StudentListItem
                {
                    StudentId = s.StudentId,
                    Program = s.Program,
                    YearLevel = s.YearLevel,
                    FullName = s.User.FirstName + " " + s.User.LastName,
                    IdNumber = s.User.IdNumber,
                    AccountStatus = s.User.AccountStatus
                })
                .ToListAsync(ct);

            var programs = await _students.GetStudents()
                .Select(s => s.Program)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync(ct);

            var yearLevels = await _students.GetStudents()
                .Select(s => s.YearLevel)
                .Where(y => !string.IsNullOrEmpty(y))
                .Distinct()
                .OrderBy(y => y)
                .ToListAsync(ct);

            return new ManageAccountsResult
            {
                Students = rows,
                Programs = programs,
                YearLevels = yearLevels,
                Filters = filters
            };
        }

        public async Task<ManageAccountsResult> GetTeachersAsync(ManageAccountsFilters filters, CancellationToken ct)
        {
            var query = _teachers.GetTeachersWithUser();

            if (!string.IsNullOrWhiteSpace(filters.Name))
            {
                var name = filters.Name.Trim().ToLower();
                query = query.Where(t => (t.User.FirstName + " " + t.User.LastName).ToLower().Contains(name));
            }

            var rows = await query
                .OrderBy(t => t.User.LastName)
                .ThenBy(t => t.User.FirstName)
                .Select(t => new TeacherListItem
                {
                    TeacherId = t.TeacherId,
                    FullName = t.User.FirstName + " " + t.User.LastName,
                    Position = t.Position
                })
                .ToListAsync(ct);

            return new ManageAccountsResult
            {
                Teachers = rows,
                Filters = filters
            };
        }

        // generate templates

        public (byte[] Content, string ContentType, string FileName) GenerateStudentsTemplate()
        {
            var headers = new[]
            {
                "FirstName","LastName","Email",
                "MiddleName","Suffix","MobileNo","HomeAddress","Province","Municipality","Barangay",
                "DateOfBirth","PlaceOfBirth","Age","MaritalStatus","Gender","Religion","Citizenship",
                "AdmissionType","Program","Department","YearLevel"
            };

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("StudentsTemplate");

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Students_Bulk_Template.xlsx");
        }

        public (byte[] Content, string ContentType, string FileName) GenerateTeachersTemplate()
        {
            var headers = new[]
            {
                "FirstName","LastName","Email",
                "MiddleName","Suffix","MobileNo","HomeAddress","Province","Municipality","Barangay",
                "DateOfBirth","PlaceOfBirth","Age","MaritalStatus","Gender","Religion","Citizenship",
                "Position"
            };

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("TeachersTemplate");

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Teachers_Bulk_Template.xlsx");
        }

        // import
        public async Task<ImportResult> ImportStudentsAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct)
        {
            var result = new ImportResult();
            if (file == null || file.Length == 0)
                return Fail("Please choose a non-empty .xlsx file.");

            var (ws, map) = ReadWorksheetAndHeaderMap(file);

            var required = new[] { "FirstName", "LastName", "Email", "Program", "YearLevel" };
            var missing = required.Where(h => !map.ContainsKey(h)).ToList();
            if (missing.Any())
                return Fail("Missing required columns: " + string.Join(", ", missing));

            var firstRow = 2;
            var lastRow = ws.LastRowUsed().RowNumber();

            for (int r = firstRow; r <= lastRow; r++)
            {
                try
                {
                    var email = Get(ws, r, map, "Email");
                    if (string.IsNullOrWhiteSpace(email))
                        continue; 

                    await _uow.BeginTransactionAsync(ct);

                    var lastName = Get(ws, r, map, "LastName");
                    var idNumber = await GenerateUniqueIdNumberAsync(prefix: '2', ct);
                    var password = GenerateHashPassword(lastName, idNumber);
                    var now = DateTime.UtcNow;

                    var user = new User
                    {
                        FirstName = Get(ws, r, map, "FirstName"),
                        LastName = lastName,
                        Email = email,
                        IdNumber = idNumber,
                        Password = password,
                        Role = "Student",
                        AccountStatus = "Active",
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    ValidateUserRequired(user, r);

                    var dup = await _users.GetUsers()
                        .AnyAsync(u => u.Email == user.Email || u.IdNumber == user.IdNumber, ct);
                    if (dup) throw new Exception($"Row {r}: Duplicate Email/ID Number.");

                    _users.AddUser(user);
                    await _uow.SaveChangesAsync(ct); 

                    var profile = new UserProfile
                    {
                        UserId = user.UserId,
                        MiddleName = Get(ws, r, map, "MiddleName"),
                        Suffix = Get(ws, r, map, "Suffix"),
                        MobileNo = Get(ws, r, map, "MobileNo"),
                        HomeAddress = Get(ws, r, map, "HomeAddress"),
                        Province = Get(ws, r, map, "Province"),
                        Municipality = Get(ws, r, map, "Municipality"),
                        Barangay = Get(ws, r, map, "Barangay"),
                        DateOfBirth = GetDateOnly(ws, r, map, "DateOfBirth"),
                        PlaceOfBirth = Get(ws, r, map, "PlaceOfBirth"),
                        Age = GetInt(ws, r, map, "Age") ?? 0,   
                        MaritalStatus = Get(ws, r, map, "MaritalStatus"),
                        Gender = Get(ws, r, map, "Gender"),
                        Religion = Get(ws, r, map, "Religion"),
                        Citizenship = Get(ws, r, map, "Citizenship")
                    };
                    _profiles.AddUserProfile(profile);

                    var student = new Student
                    {
                        AdmissionType = Get(ws, r, map, "AdmissionType"),
                        Program = Get(ws, r, map, "Program"),
                        Department = Get(ws, r, map, "Department"),
                        YearLevel = Get(ws, r, map, "YearLevel"),
                        StudentStatus = "Enrolled",
                        UserId = user.UserId
                    };

                    if (string.IsNullOrWhiteSpace(student.Program) || string.IsNullOrWhiteSpace(student.YearLevel))
                        throw new Exception($"Row {r}: Program/YearLevel required.");

                    _students.AddStudent(student);

                    await _uow.CommitAsync(ct);
                    result.InsertedCount++;
                }
                catch (Exception ex)
                {
                    await _uow.RollbackAsync(ct);
                    result.FailedCount++;
                    if (result.FirstError == null) result.FirstError = ex.Message;
                }
            }

            return result;
            static ImportResult Fail(string msg) => new() { FailedCount = 1, FirstError = msg };
        }

        public async Task<ImportResult> ImportTeachersAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct)
        {
            var result = new ImportResult();
            if (file == null || file.Length == 0)
                return Fail("Please choose a non-empty .xlsx file.");

            var (ws, map) = ReadWorksheetAndHeaderMap(file);

            var required = new[] { "FirstName", "LastName", "Email", "Position" };
            var missing = required.Where(h => !map.ContainsKey(h)).ToList();
            if (missing.Any())
                return Fail("Missing required columns: " + string.Join(", ", missing));

            var firstRow = 2;
            var lastRow = ws.LastRowUsed().RowNumber();

            for (int r = firstRow; r <= lastRow; r++)
            {
                try
                {
                    var email = Get(ws, r, map, "Email");
                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    await _uow.BeginTransactionAsync(ct);

                    var lastName = Get(ws, r, map, "LastName");
                    var idNumber = await GenerateUniqueIdNumberAsync(prefix: '1', ct);
                    var password = GenerateHashPassword(lastName, idNumber);
                    var now = DateTime.UtcNow;

                    var user = new User
                    {
                        FirstName = Get(ws, r, map, "FirstName"),
                        LastName = lastName,
                        Email = email,
                        IdNumber = idNumber,
                        Password = password,
                        Role = "Teacher",
                        AccountStatus = "Active",
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    ValidateUserRequired(user, r);

                    var dup = await _users.GetUsers()
                        .AnyAsync(u => u.Email == user.Email || u.IdNumber == user.IdNumber, ct);
                    if (dup) throw new Exception($"Row {r}: Duplicate Email/ID Number.");

                    _users.AddUser(user);
                    await _uow.SaveChangesAsync(ct);

                    var profile = new UserProfile
                    {
                        UserId = user.UserId,
                        MiddleName = Get(ws, r, map, "MiddleName"),
                        Suffix = Get(ws, r, map, "Suffix"),
                        MobileNo = Get(ws, r, map, "MobileNo"),
                        HomeAddress = Get(ws, r, map, "HomeAddress"),
                        Province = Get(ws, r, map, "Province"),
                        Municipality = Get(ws, r, map, "Municipality"),
                        Barangay = Get(ws, r, map, "Barangay"),
                        DateOfBirth = GetDateOnly(ws, r, map, "DateOfBirth"),
                        PlaceOfBirth = Get(ws, r, map, "PlaceOfBirth"),
                        Age = GetInt(ws, r, map, "Age") ?? 0,  
                        MaritalStatus = Get(ws, r, map, "MaritalStatus"),
                        Gender = Get(ws, r, map, "Gender"),
                        Religion = Get(ws, r, map, "Religion"),
                        Citizenship = Get(ws, r, map, "Citizenship")
                    };
                    _profiles.AddUserProfile(profile);

                    var teacher = new Teacher
                    {
                        Position = Get(ws, r, map, "Position"),
                        UserId = user.UserId
                    };
                    _teachers.AddTeacher(teacher);

                    await _uow.CommitAsync(ct);
                    result.InsertedCount++;
                }
                catch (Exception ex)
                {
                    await _uow.RollbackAsync(ct);
                    result.FailedCount++;
                    if (result.FirstError == null) result.FirstError = ex.Message;
                }
            }

            return result;
            static ImportResult Fail(string msg) => new() { FailedCount = 1, FirstError = msg };
        }

        // helpers
        private static (IXLWorksheet ws, Dictionary<string, int> map) ReadWorksheetAndHeaderMap(IFormFile file)
        {
            var stream = file.OpenReadStream();
            var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = ws.LastColumnUsed().ColumnNumber();
            for (int c = 1; c <= lastCol; c++)
            {
                var name = ws.Cell(1, c).GetString().Trim();
                if (!string.IsNullOrEmpty(name)) map[name] = c;
            }
            return (ws, map);
        }

        private static string Get(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
            => map.TryGetValue(name, out var c) ? ws.Cell(r, c).GetString().Trim() : string.Empty;

        private static string Fallback(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static DateTime GetDate(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
        {
            if (!map.TryGetValue(name, out var c)) return default;
            var cell = ws.Cell(r, c);
            if (cell.DataType == XLDataType.DateTime && cell.GetDateTime().Year > 1900) return cell.GetDateTime();
            return DateTime.TryParse(cell.GetString(), out var dt) ? dt : default;
        }

        private static int? GetInt(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
        {
            if (!map.TryGetValue(name, out var c)) return null;
            var s = ws.Cell(r, c).GetString().Trim();
            return int.TryParse(s, out var v) ? v : null;
        }

        private static void ValidateUserRequired(User u, int row)
        {
            if (string.IsNullOrWhiteSpace(u.FirstName) ||
                string.IsNullOrWhiteSpace(u.LastName) ||
                string.IsNullOrWhiteSpace(u.Email) ||
                string.IsNullOrWhiteSpace(u.IdNumber))
                throw new Exception($"Row {row}: Missing key user fields.");
        }

        private async Task<string> GenerateUniqueIdNumberAsync(char prefix, CancellationToken ct)
        {
            for (int i = 0; i < 20; i++)
            {
                var candidate = GenerateIdNumber(prefix);
                var exists = await _users.GetUsers().AnyAsync(u => u.IdNumber == candidate, ct);
                if (!exists) return candidate;
            }
            throw new Exception("Failed to generate a unique ID number.");
        }

        private static string GenerateIdNumber(char prefix)
        {
            var rng = new Random();
            int n = rng.Next(0, 10_000_000); 
            return prefix + n.ToString("D7");
        }

        private static string GenerateHashPassword(string lastName, string idNumber)
        {
            var ln = (lastName ?? string.Empty).Trim();
            var last4 = string.IsNullOrEmpty(idNumber) ? string.Empty
                      : (idNumber.Length >= 4 ? idNumber[^4..] : idNumber);
            var plain = $"{ln}{last4}";
            return PasswordManager.EncryptPassword(plain);
        }

        private static DateOnly? GetDateOnly(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
        {
            if (!map.TryGetValue(name, out var c))
                return null;

            var cell = ws.Cell(r, c);

            // If Excel stored it as a date/time
            if (cell.DataType == XLDataType.DateTime)
            {
                var dt = cell.GetDateTime();
                if (dt.Year > 1900 && dt.TimeOfDay == TimeSpan.Zero)
                    return DateOnly.FromDateTime(dt);

                return null;
            }

            // If Excel stored it as text, check if it's a pure date
            var s = cell.GetString().Trim();
            if (string.IsNullOrEmpty(s))
                return null;

            if (DateTime.TryParse(s, out var parsed))
            {
                // Only accept if it has no time part (00:00:00)
                if (parsed.Year > 1900 && parsed.TimeOfDay == TimeSpan.Zero)
                    return DateOnly.FromDateTime(parsed);
            }

            return null;
        }
    }
}
