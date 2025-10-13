using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ClosedXML.Excel;
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
    public class AdminCreateAccountService : IAdminCreateAccountService
    {
        private readonly IStudentRepository _students;
        private readonly ITeacherRepository _teachers;
        private readonly IUserRepository _users;
        private readonly IUserProfileRepository _profiles;
        private readonly IUnitOfWork _uow;

        public AdminCreateAccountService(
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

        // add singe record:

        public async Task<(int UserId, string IdNumber)> CreateSingleStudentAsync(
        StudentProfileViewModel vm,
        ImportUserDefaults defaults,
        CancellationToken ct)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (string.IsNullOrWhiteSpace(vm.LastName)) throw new Exception("LastName is required.");
            if (string.IsNullOrWhiteSpace(vm.FirstName)) throw new Exception("FirstName is required.");
            if (string.IsNullOrWhiteSpace(vm.Email)) throw new Exception("Email is required.");
            if (string.IsNullOrWhiteSpace(vm.AdmissionType)) throw new Exception("AdmissionType is required.");
            if (string.IsNullOrWhiteSpace(vm.Section)) throw new Exception("Section is required.");
            if (string.IsNullOrWhiteSpace(vm.Program) || string.IsNullOrWhiteSpace(vm.YearLevel))
                throw new Exception("Program and YearLevel are required.");

            await _uow.BeginTransactionAsync(ct);

            try
            {
                var idNumber = await GenerateUniqueIdNumberAsync('2', ct);
                var password = GenerateHashPassword(vm.LastName, idNumber);
                var now = DateTime.Now;

                var user = new User
                {
                    FirstName = vm.FirstName,
                    LastName = vm.LastName,
                    Email = vm.Email,
                    IdNumber = idNumber,
                    Password = password,
                    Role = "Student",
                    AccountStatus = defaults?.DefaultAccountStatus ?? "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _users.AddUser(user);
                await _uow.SaveChangesAsync(ct); // get UserId

                var profile = new UserProfile
                {
                    UserId = user.UserId,
                    MiddleName = vm.MiddleName,
                    Suffix = vm.Suffix,
                    MobileNo = vm.MobileNo,
                    HomeAddress = vm.HomeAddress,
                    Province = vm.Province,
                    Municipality = vm.Municipality,
                    Barangay = vm.Barangay,
                    DateOfBirth = vm.DateOfBirth,
                    PlaceOfBirth = vm.PlaceOfBirth,
                    Age = vm.Age ?? 0,
                    MaritalStatus = vm.MaritalStatus,
                    Gender = vm.Gender,
                    Religion = vm.Religion,
                    Citizenship = vm.Citizenship
                };

                _profiles.AddUserProfile(profile);
                await _uow.SaveChangesAsync(ct);
                var program = ProgramShortcut(vm.Program);
                var admissionType = AdmissionTypeShortcut(vm.AdmissionType);

                var student = new Student
                {
                    UserId = user.UserId,
                    AdmissionType = admissionType,
                    Program = program,
                    Department = vm.Department,
                    YearLevel = vm.YearLevel,
                    Section = vm.Section
                };

                _students.AddStudent(student);
                await _uow.SaveChangesAsync(ct);

                await _uow.CommitAsync(ct);
                return (user.UserId, idNumber);
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<(int UserId, string IdNumber)> CreateSingleTeacherAsync(
            TeacherProfileViewModel vm,
            ImportUserDefaults defaults,
            CancellationToken ct)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (string.IsNullOrWhiteSpace(vm.LastName)) throw new Exception("LastName is required.");
            if (string.IsNullOrWhiteSpace(vm.FirstName)) throw new Exception("FirstName is required.");
            if (string.IsNullOrWhiteSpace(vm.Email)) throw new Exception("Email is required.");

            await _uow.BeginTransactionAsync(ct);

            try
            {
                var idNumber = await GenerateUniqueIdNumberAsync('1', ct);
                var password = GenerateHashPassword(vm.LastName, idNumber);
                var now = DateTime.Now;

                var user = new User
                {
                    FirstName = vm.FirstName,
                    LastName = vm.LastName,
                    Email = vm.Email,
                    IdNumber = idNumber,
                    Password = password,
                    Role = "Teacher",
                    AccountStatus = defaults?.DefaultAccountStatus ?? "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _users.AddUser(user);
                await _uow.SaveChangesAsync(ct);

                var profile = new UserProfile
                {
                    UserId = user.UserId,
                    MiddleName = vm.MiddleName,
                    Suffix = vm.Suffix,
                    MobileNo = vm.MobileNo,
                    HomeAddress = vm.HomeAddress,
                    Province = vm.Province,
                    Municipality = vm.Municipality,
                    Barangay = vm.Barangay,
                    DateOfBirth = vm.DateOfBirth,
                    PlaceOfBirth = vm.PlaceOfBirth,
                    Age = vm.Age ?? 0,
                    MaritalStatus = vm.MaritalStatus,
                    Gender = vm.Gender,
                    Religion = vm.Religion,
                    Citizenship = vm.Citizenship
                };

                _profiles.AddUserProfile(profile);
                await _uow.SaveChangesAsync(ct);

                var teacher = new Teacher
                {
                    UserId = user.UserId,
                    Position = string.IsNullOrWhiteSpace(vm.Position)
                        ? (defaults?.DefaultTeacherPosition ?? "Instructor")
                        : vm.Position
                };

                _teachers.AddTeacher(teacher);
                await _uow.SaveChangesAsync(ct);

                await _uow.CommitAsync(ct);
                return (user.UserId, idNumber);
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }

        // generating templates:

        public (byte[] Content, string ContentType, string FileName) GenerateStudentsTemplate()
        {
            var headers = new[]
            {
                "FirstName","LastName","Email",
                "MiddleName","Suffix","MobileNo","HomeAddress","Province","Municipality","Barangay",
                "DateOfBirth","PlaceOfBirth","Age","MaritalStatus","Gender","Religion","Citizenship",
                "AdmissionType","Program","Department","YearLevel","Section"
            };

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("StudentsTemplate");
            for (int c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
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
            for (int c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Teachers_Bulk_Template.xlsx");
        }

        // imports:

        public async Task<ImportResult> ImportStudentsAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct)
        {
            var result = new ImportResult();
            if (file == null || file.Length == 0)
                return new ImportResult { FailedCount = 1, FirstError = "Please choose a non-empty .xlsx file." };

            Console.WriteLine("=== ImportStudents started ===");
            var (ws, map) = ReadWorksheetAndHeaderMap(file);

            // Require headers (AdmissionType included)
            Console.WriteLine("Headers: " + string.Join(", ", map.Keys));
            var required = new[] { "FirstName", "LastName", "Email", "Program", "YearLevel", "AdmissionType", "Section" };
            var missing = required.Where(h => !map.ContainsKey(h)).ToList();
            if (missing.Any())
            {
                var msg = "Missing required columns: " + string.Join(", ", missing);
                Console.WriteLine(msg);
                return new ImportResult { FailedCount = 1, FirstError = msg };
            }

            var firstRow = 2;
            var lastRow = ws.LastRowUsed().RowNumber();
            Console.WriteLine($"Detected rows: {firstRow} to {lastRow}");

            for (int r = firstRow; r <= lastRow; r++)
            {
                try
                {
                    Console.WriteLine($"[Row {r}] Starting...");

                    var email = Get(ws, r, map, "Email");
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        Console.WriteLine($"[Row {r}] Skipped: no email");
                        continue;
                    }

                    await _uow.BeginTransactionAsync(ct);
                    Console.WriteLine($"[Row {r}] Transaction started");

                    var lastName = Get(ws, r, map, "LastName");
                    var idNumber = await GenerateUniqueIdNumberAsync('2', ct);
                    Console.WriteLine($"[Row {r}] Generated ID number: {idNumber}");

                    var password = GenerateHashPassword(lastName, idNumber);
                    var now = DateTime.Now; // match PostgreSQL 'timestamp without time zone'

                    var user = new User
                    {
                        FirstName = Get(ws, r, map, "FirstName"),
                        LastName = lastName,
                        Email = email,
                        IdNumber = idNumber,
                        Password = password,
                        Role = "Student",
                        AccountStatus = defaults?.DefaultAccountStatus ?? "Active",
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    Console.WriteLine($"[Row {r}] Adding user...");
                    _users.AddUser(user);
                    await _uow.SaveChangesAsync(ct);
                    Console.WriteLine($"[Row {r}] User saved with ID={user.UserId}");

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
                    Console.WriteLine($"[Row {r}] Profile added");
                    await _uow.SaveChangesAsync(ct); 

                    var program = Get(ws, r, map, "Program");
                    var yearLevel = Get(ws, r, map, "YearLevel");
                    var department = Get(ws, r, map, "Department");
                    var admissionType = Get(ws, r, map, "AdmissionType");
                    var section = Get(ws, r, map, "Section");

                    program = ProgramShortcut(program);
                    admissionType = AdmissionTypeShortcut(admissionType);

                    if (string.IsNullOrWhiteSpace(program) || string.IsNullOrWhiteSpace(yearLevel))
                        throw new Exception($"Row {r}: Program/YearLevel required.");
                    if (string.IsNullOrWhiteSpace(admissionType))
                        throw new Exception($"Row {r}: AdmissionType required.");

                    Console.WriteLine($"[Row {r}] Student about to insert: AdmissionType='{admissionType}', Program='{program}', YearLevel='{yearLevel}', Dept='{department}'");

                    var student = new Student
                    {
                        UserId = user.UserId,
                        AdmissionType = admissionType,
                        Program = program,
                        Department = department,
                        YearLevel = yearLevel,
                        Section = section
                    };
                    _students.AddStudent(student);
                    await _uow.SaveChangesAsync(ct); // persist student now
                    Console.WriteLine($"[Row {r}] Student added");

                    await _uow.CommitAsync(ct);
                    Console.WriteLine($"[Row {r}] Transaction committed");
                    result.InsertedCount++;
                }
                catch (Exception ex)
                {
                    await _uow.RollbackAsync(ct);
                    Console.WriteLine($"[Row {r}] ERROR: {ex.Message}");
                    result.FailedCount++;
                    if (result.FirstError == null) result.FirstError = ex.Message;
                }
            }

            Console.WriteLine($"=== ImportStudents finished. Inserted={result.InsertedCount}, Failed={result.FailedCount} ===");
            return result;
        }

        public async Task<ImportResult> ImportTeachersAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct)
        {
            var result = new ImportResult();
            if (file == null || file.Length == 0)
                return new ImportResult { FailedCount = 1, FirstError = "Please choose a non-empty .xlsx file." };

            Console.WriteLine("=== ImportTeachers started ===");
            var (ws, map) = ReadWorksheetAndHeaderMap(file);

            var firstRow = 2;
            var lastRow = ws.LastRowUsed().RowNumber();
            Console.WriteLine($"Detected rows: {firstRow} to {lastRow}");

            for (int r = firstRow; r <= lastRow; r++)
            {
                try
                {
                    Console.WriteLine($"[Row {r}] Starting...");

                    var email = Get(ws, r, map, "Email");
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        Console.WriteLine($"[Row {r}] Skipped: no email");
                        continue;
                    }

                    await _uow.BeginTransactionAsync(ct);
                    Console.WriteLine($"[Row {r}] Transaction started");

                    var lastName = Get(ws, r, map, "LastName");
                    var idNumber = await GenerateUniqueIdNumberAsync('1', ct);
                    Console.WriteLine($"[Row {r}] Generated ID number: {idNumber}");

                    var password = GenerateHashPassword(lastName, idNumber);
                    var now = DateTime.Now; // match PostgreSQL 'timestamp without time zone'

                    var user = new User
                    {
                        FirstName = Get(ws, r, map, "FirstName"),
                        LastName = lastName,
                        Email = email,
                        IdNumber = idNumber,
                        Password = password,
                        Role = "Teacher",
                        AccountStatus = defaults?.DefaultAccountStatus ?? "Active",
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    Console.WriteLine($"[Row {r}] Adding user...");
                    _users.AddUser(user);
                    await _uow.SaveChangesAsync(ct);
                    Console.WriteLine($"[Row {r}] User saved with ID={user.UserId}");

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
                    Console.WriteLine($"[Row {r}] Profile added");
                    await _uow.SaveChangesAsync(ct); // persist profile now

                    var teacher = new Teacher
                    {
                        UserId = user.UserId,
                        Position = Get(ws, r, map, "Position")
                    };
                    _teachers.AddTeacher(teacher);
                    await _uow.SaveChangesAsync(ct); // persist teacher now
                    Console.WriteLine($"[Row {r}] Teacher added");

                    await _uow.CommitAsync(ct);
                    Console.WriteLine($"[Row {r}] Transaction committed");
                    result.InsertedCount++;
                }
                catch (Exception ex)
                {
                    await _uow.RollbackAsync(ct);
                    Console.WriteLine($"[Row {r}] ERROR: {ex.Message}");
                    result.FailedCount++;
                    if (result.FirstError == null) result.FirstError = ex.Message;
                }
            }

            Console.WriteLine($"=== ImportTeachers finished. Inserted={result.InsertedCount}, Failed={result.FailedCount} ===");
            return result;
        }


        // helpers:

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

        private static string ProgramShortcut(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Equals("Computer Science", StringComparison.OrdinalIgnoreCase)) return "BSCS";
            if (s.Equals("Information Technology", StringComparison.OrdinalIgnoreCase)) return "BSIT";
            return s; 
        }

        private static string AdmissionTypeShortcut(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Equals("Old", StringComparison.OrdinalIgnoreCase)) return "Old Student";
            if (s.Equals("New", StringComparison.OrdinalIgnoreCase)) return "New Student";
            return s; 
        }

        private static DateOnly? GetDateOnly(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
        {
            if (!map.TryGetValue(name, out var c)) return null;

            var cell = ws.Cell(r, c);

            if (cell.DataType == XLDataType.DateTime)
            {
                var dt = cell.GetDateTime();
                if (dt.Year > 1900 && dt.TimeOfDay == TimeSpan.Zero)
                    return DateOnly.FromDateTime(dt);

                return null;
            }

            var s = cell.GetString().Trim();
            if (string.IsNullOrEmpty(s)) return null;

            if (DateTime.TryParse(s, out var parsed))
            {
                if (parsed.Year > 1900 && parsed.TimeOfDay == TimeSpan.Zero)
                    return DateOnly.FromDateTime(parsed);
            }

            return null;
        }
    }
}
