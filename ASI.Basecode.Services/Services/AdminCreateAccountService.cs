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
            if (string.IsNullOrWhiteSpace(vm.LastName)) throw new Exception("Last Name is required.");
            if (string.IsNullOrWhiteSpace(vm.FirstName)) throw new Exception("First Name is required.");
            if (string.IsNullOrWhiteSpace(vm.Email)) throw new Exception("Email is required.");
            if (string.IsNullOrWhiteSpace(vm.AdmissionType)) throw new Exception("Admission Type is required.");
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
            if (string.IsNullOrWhiteSpace(vm.LastName)) throw new Exception("Last Name is required.");
            if (string.IsNullOrWhiteSpace(vm.FirstName)) throw new Exception("First Name is required.");
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
                "First Name", "Last Name", "Middle Name", "Suffix", "Email", "Mobile No", "Admission Type",
                "Program", "Department", "Year Level", "Section", "Home Address", "Province", "Municipality",
                "Barangay", "Date Of Birth", "Age", "Place Of Birth", "Gender", "Marital Status", "Religion", "Citizenship"
            };

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("StudentsTemplate");

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            ws.SheetView.FreezeRows(1);

            int FindCol(string name) =>
                Array.FindIndex(headers, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase)) + 1;

            var emailCol = FindCol("Email");
            var mobileCol = FindCol("Mobile No");

            if (emailCol > 0)
            {
                ws.Column(emailCol).Style.NumberFormat.SetFormat("@");
                ws.Range(2, emailCol, 10000, emailCol).Style.NumberFormat.SetFormat("@");
            }

            if (mobileCol > 0)
            {
                ws.Column(mobileCol).Style.NumberFormat.SetFormat("@");
                ws.Range(2, mobileCol, 10000, mobileCol).Style.NumberFormat.SetFormat("@");
            }

            ws.Columns().AdjustToContents();

            using var ms = new System.IO.MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Students_Bulk_Template.xlsx");
        }

        public (byte[] Content, string ContentType, string FileName) GenerateTeachersTemplate()
        {
            var headers = new[]
            {
                "First Name", "Last Name", "Middle Name", "Suffix", "Email", "Mobile No", "Position",
                "Home Address", "Province", "Municipality", "Barangay", "Date Of Birth", "Age", "Place Of Birth",
                "Gender", "Marital Status", "Religion", "Citizenship"
            };

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("TeachersTemplate");

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            ws.SheetView.FreezeRows(1);

            int FindCol(string name) =>
                Array.FindIndex(headers, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase)) + 1;

            var emailCol = FindCol("Email");
            var mobileCol = FindCol("Mobile No");

            if (emailCol > 0)
            {
                ws.Column(emailCol).Style.NumberFormat.SetFormat("@");
                ws.Range(2, emailCol, 10000, emailCol).Style.NumberFormat.SetFormat("@");
            }

            if (mobileCol > 0)
            {
                ws.Column(mobileCol).Style.NumberFormat.SetFormat("@");
                ws.Range(2, mobileCol, 10000, mobileCol).Style.NumberFormat.SetFormat("@");
            }

            ws.Columns().AdjustToContents();

            using var ms = new System.IO.MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Teachers_Bulk_Template.xlsx");
        }


        // imports:
        public async Task<ImportResult> ImportStudentsAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct)
        {
            var result = new ImportResult();

            // Validate file exists
            if (file == null || file.Length == 0)
            {
                result.FailedCount = 1;
                result.FirstError = "No file was selected. Please choose a file and try again.";
                return result;
            }

            // Validate file type
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                result.FailedCount = 1;
                result.FirstError = "Please upload an Excel file (.xlsx format only).";
                return result;
            }

            // Validate file size (10MB limit)
            if (file.Length > 10 * 1024 * 1024)
            {
                result.FailedCount = 1;
                result.FirstError = "The file is too large. Please upload a file smaller than 10MB.";
                return result;
            }

            try
            {
                var (ws, map) = ReadWorksheetAndHeaderMap(file);

                var requiredHeaders = new[]
                {
                     "First Name", "Last Name", "Email", "Admission Type", "Program", "Year Level", "Section", "Date Of Birth", "Age"
                };

                var missingHeaders = requiredHeaders.Where(h => !map.ContainsKey(h)).ToList();
                if (missingHeaders.Any())
                {
                    result.FailedCount = 1;
                    result.FirstError = $"Some required columns are missing: {string.Join(", ", missingHeaders)}. Please use the template provided.";
                    return result;
                }

                var firstRow = 2;
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                if (lastRow < firstRow)
                {
                    result.FailedCount = 1;
                    result.FirstError = "The file doesn't have any student data. Please add student information below the header row.";
                    return result;
                }

                for (int r = firstRow; r <= lastRow; r++)
                {
                    try
                    {
                        // Check if row is completely empty
                        var isEmptyRow = true;
                        foreach (var col in map.Values)
                        {
                            if (!string.IsNullOrWhiteSpace(ws.Cell(r, col).GetString()))
                            {
                                isEmptyRow = false;
                                break;
                            }
                        }

                        if (isEmptyRow)
                            continue; // Skip empty rows

                        // Get all required fields
                        var firstName = Get(ws, r, map, "First Name");
                        var lastName = Get(ws, r, map, "Last Name");
                        var email = GetEmail(ws, r, map, "Email");
                        var admissionType = Get(ws, r, map, "Admission Type");
                        var program = Get(ws, r, map, "Program");
                        var yearLevel = Get(ws, r, map, "Year Level");
                        var section = Get(ws, r, map, "Section");
                        var dateOfBirth = GetDateOnly(ws, r, map, "Date Of Birth");
                        var age = GetInt(ws, r, map, "Age");

                        // Validate required user fields
                        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email))
                            throw new Exception($"Row {r}: First Name, Last Name, and Email are required for all students.");

                        // Validate email format
                        if (!IsValidEmail(email))
                            throw new Exception($"Row {r}: '{email}' doesn't look like a valid email address.");

                        // Validate required student fields
                        if (string.IsNullOrWhiteSpace(admissionType) || string.IsNullOrWhiteSpace(program) || string.IsNullOrWhiteSpace(yearLevel))
                            throw new Exception($"Row {r}: Admission Type, Program, and Year Level must be filled in.");

                        if (string.IsNullOrWhiteSpace(section))
                            throw new Exception($"Row {r}: Section is required.");

                        // Validate date and age
                        if (!dateOfBirth.HasValue)
                            throw new Exception($"Row {r}: Date of Birth must be a valid date (e.g., 01/15/2000).");

                        if (!age.HasValue || age.Value < 1 || age.Value > 100)
                            throw new Exception($"Row {r}: Age must be a number between 1 and 100.");

                        // Check for duplicate email
                        var emailExists = await _users.GetUsers().AnyAsync(u => u.Email == email, ct);
                        if (emailExists)
                            throw new Exception($"Row {r}: A student with email '{email}' already exists in the system.");

                        await _uow.BeginTransactionAsync(ct);

                        var idNumber = await GenerateUniqueIdNumberAsync('2', ct);
                        var password = GenerateHashPassword(lastName, idNumber);
                        var now = DateTime.Now;

                        // Create User
                        var user = new User
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            IdNumber = idNumber,
                            Password = password,
                            Role = "Student",
                            AccountStatus = defaults?.DefaultAccountStatus ?? "Active",
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        _users.AddUser(user);
                        await _uow.SaveChangesAsync(ct);

                        // Create Profile
                        var profile = new UserProfile
                        {
                            UserId = user.UserId,
                            MiddleName = Get(ws, r, map, "Middle Name"),
                            Suffix = Get(ws, r, map, "Suffix"),
                            MobileNo = Get(ws, r, map, "Mobile No"),
                            HomeAddress = Get(ws, r, map, "Home Address"),
                            Province = Get(ws, r, map, "Province"),
                            Municipality = Get(ws, r, map, "Municipality"),
                            Barangay = Get(ws, r, map, "Barangay"),
                            DateOfBirth = dateOfBirth.Value,
                            PlaceOfBirth = Get(ws, r, map, "Place Of Birth"),
                            Age = age.Value,
                            MaritalStatus = Get(ws, r, map, "Marital Status"),
                            Gender = Get(ws, r, map, "Gender"),
                            Religion = Get(ws, r, map, "Religion"),
                            Citizenship = Get(ws, r, map, "Citizenship")
                        };

                        _profiles.AddUserProfile(profile);
                        await _uow.SaveChangesAsync(ct);

                        // Create Student
                        var student = new Student
                        {
                            UserId = user.UserId,
                            AdmissionType = AdmissionTypeShortcut(admissionType),
                            Program = ProgramShortcut(program),
                            Department = Get(ws, r, map, "Department"),
                            YearLevel = yearLevel,
                            Section = section
                        };

                        _students.AddStudent(student);
                        await _uow.SaveChangesAsync(ct);

                        await _uow.CommitAsync(ct);
                        result.InsertedCount++;
                    }
                    catch (Exception ex)
                    {
                        await _uow.RollbackAsync(ct);
                        result.FailedCount++;
                        if (result.FirstError == null)
                            result.FirstError = ex.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailedCount = 1;
                if (ex.Message.Contains("not find any worksheet"))
                {
                    result.FirstError = "The file appears to be corrupted or empty. Please download a fresh template and try again.";
                }
                else if (ex.Message.Contains("file format"))
                {
                    result.FirstError = "We couldn't read this file. Make sure it's a valid Excel file (.xlsx).";
                }
                else
                {
                    result.FirstError = "Something went wrong while reading the file. Please check the file and try again.";
                }
            }

            return result;
        }

        // Add email validation helper
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ImportResult> ImportTeachersAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct)
        {
            var result = new ImportResult();

            // Validate file exists
            if (file == null || file.Length == 0)
            {
                result.FailedCount = 1;
                result.FirstError = "No file was selected. Please choose a file and try again.";
                return result;
            }

            // Validate file type
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                result.FailedCount = 1;
                result.FirstError = "Please upload an Excel file (.xlsx format only).";
                return result;
            }

            // Validate file size (10MB limit)
            if (file.Length > 10 * 1024 * 1024)
            {
                result.FailedCount = 1;
                result.FirstError = "The file is too large. Please upload a file smaller than 10MB.";
                return result;
            }

            try
            {
                var (ws, map) = ReadWorksheetAndHeaderMap(file);

                var requiredHeaders = new[]
                {
                    "First Name", "Last Name", "Email", "Position", "Date Of Birth", "Age"
                };

                var missingHeaders = requiredHeaders.Where(h => !map.ContainsKey(h)).ToList();
                if (missingHeaders.Any())
                {
                    result.FailedCount = 1;
                    result.FirstError = $"Some required columns are missing: {string.Join(", ", missingHeaders)}. Please use the template provided.";
                    return result;
                }

                var firstRow = 2;
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                if (lastRow < firstRow)
                {
                    result.FailedCount = 1;
                    result.FirstError = "The file doesn't have any teacher data. Please add teacher information below the header row.";
                    return result;
                }

                for (int r = firstRow; r <= lastRow; r++)
                {
                    try
                    {
                        // Check if row is completely empty
                        var isEmptyRow = true;
                        foreach (var col in map.Values)
                        {
                            if (!string.IsNullOrWhiteSpace(ws.Cell(r, col).GetString()))
                            {
                                isEmptyRow = false;
                                break;
                            }
                        }

                        if (isEmptyRow)
                            continue;

                        // Get all required fields
                        var firstName = Get(ws, r, map, "First Name");
                        var lastName = Get(ws, r, map, "Last Name");
                        var email = GetEmail(ws, r, map, "Email");
                        var position = Get(ws, r, map, "Position");
                        var dateOfBirth = GetDateOnly(ws, r, map, "Date Of Birth");
                        var age = GetInt(ws, r, map, "Age");

                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email))
                            throw new Exception($"Row {r}: First Name, Last Name, and Email are required for all teachers.");

                        // Validate email format
                        if (!IsValidEmail(email))
                            throw new Exception($"Row {r}: '{email}' doesn't look like a valid email address.");

                        if (string.IsNullOrWhiteSpace(position))
                            throw new Exception($"Row {r}: Position is required.");

                        // Validate date and age
                        if (!dateOfBirth.HasValue)
                            throw new Exception($"Row {r}: Date of Birth must be a valid date (e.g., 01/15/1980).");

                        if (!age.HasValue || age.Value < 18 || age.Value > 100)
                            throw new Exception($"Row {r}: Age must be a number between 18 and 100.");

                        // Check for duplicate email
                        var emailExists = await _users.GetUsers().AnyAsync(u => u.Email == email, ct);
                        if (emailExists)
                            throw new Exception($"Row {r}: A teacher with email '{email}' already exists in the system.");

                        await _uow.BeginTransactionAsync(ct);

                        var idNumber = await GenerateUniqueIdNumberAsync('1', ct);
                        var password = GenerateHashPassword(lastName, idNumber);
                        var now = DateTime.Now;

                        // Create User
                        var user = new User
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            IdNumber = idNumber,
                            Password = password,
                            Role = "Teacher",
                            AccountStatus = defaults?.DefaultAccountStatus ?? "Active",
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        _users.AddUser(user);
                        await _uow.SaveChangesAsync(ct);

                        // Create Profile
                        var profile = new UserProfile
                        {
                            UserId = user.UserId,
                            MiddleName = Get(ws, r, map, "Middle Name"),
                            Suffix = Get(ws, r, map, "Suffix"),
                            MobileNo = Get(ws, r, map, "Mobile No"),
                            HomeAddress = Get(ws, r, map, "Home Address"),
                            Province = Get(ws, r, map, "Province"),
                            Municipality = Get(ws, r, map, "Municipality"),
                            Barangay = Get(ws, r, map, "Barangay"),
                            DateOfBirth = dateOfBirth.Value,
                            PlaceOfBirth = Get(ws, r, map, "Place Of Birth"),
                            Age = age.Value,
                            MaritalStatus = Get(ws, r, map, "Marital Status"),
                            Gender = Get(ws, r, map, "Gender"),
                            Religion = Get(ws, r, map, "Religion"),
                            Citizenship = Get(ws, r, map, "Citizenship")
                        };

                        _profiles.AddUserProfile(profile);
                        await _uow.SaveChangesAsync(ct);

                        // Create Teacher
                        var teacher = new Teacher
                        {
                            UserId = user.UserId,
                            Position = position
                        };

                        _teachers.AddTeacher(teacher);
                        await _uow.SaveChangesAsync(ct);

                        await _uow.CommitAsync(ct);
                        result.InsertedCount++;
                    }
                    catch (Exception ex)
                    {
                        await _uow.RollbackAsync(ct);
                        result.FailedCount++;
                        if (result.FirstError == null)
                            result.FirstError = ex.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                result.FailedCount = 1;
                if (ex.Message.Contains("not find any worksheet"))
                {
                    result.FirstError = "The file appears to be corrupted or empty. Please download a fresh template and try again.";
                }
                else if (ex.Message.Contains("file format"))
                {
                    result.FirstError = "We couldn't read this file. Make sure it's a valid Excel file (.xlsx).";
                }
                else
                {
                    result.FirstError = "Something went wrong while reading the file. Please check the file and try again.";
                }
            }

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
                var header = ws.Cell(1, c).GetString().Trim();

                var normalizedHeader = NormalizeHeaderName(header);

                if (!string.IsNullOrEmpty(normalizedHeader))
                {
                    map[normalizedHeader] = c;
                }
            }
            return (ws, map);
        }

        private static string NormalizeHeaderName(string header)
        {
            var normalizedHeader = System.Text.RegularExpressions.Regex.Replace(header, "([a-z])([A-Z])", "$1 $2");
            return normalizedHeader;
        }

        private static string Get(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
            => map.TryGetValue(name, out var c) ? ws.Cell(r, c).GetString().Trim() : string.Empty;

        private static int? GetInt(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
        {
            if (!map.TryGetValue(name, out var c)) return null;
            var s = ws.Cell(r, c).GetString().Trim();
            return int.TryParse(s, out var v) ? v : null;
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

        private static string GetEmail(IXLWorksheet ws, int r, IDictionary<string, int> map, string name)
        {
            if (!map.TryGetValue(name, out var c)) return string.Empty;

            var cell = ws.Cell(r, c);
            string s = cell.GetString()?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(cell.FormulaA1) &&
                cell.FormulaA1.StartsWith("HYPERLINK(", StringComparison.OrdinalIgnoreCase))
            {
                var f = cell.FormulaA1;
                // Extract inside of HYPERLINK(...)
                var inner = f.Substring(10, f.Length - 11);

                // Split arguments by the first comma outside quotes
                int depth = 0, splitIdx = -1;
                for (int i = 0; i < inner.Length; i++)
                {
                    char ch = inner[i];
                    if (ch == '"') depth = 1 - depth;
                    else if (ch == ',' && depth == 0)
                    {
                        splitIdx = i;
                        break;
                    }
                }

                if (splitIdx != -1 && splitIdx + 1 < inner.Length)
                {
                    var display = inner.Substring(splitIdx + 1).Trim();
                    if (display.StartsWith("\"") && display.EndsWith("\"") && display.Length >= 2)
                        display = display.Substring(1, display.Length - 2); // remove quotes

                    if (!string.IsNullOrWhiteSpace(display))
                        s = display;
                }
                else
                {
                    // If formula only contains the mailto part, extract it
                    var parts = inner.Split(',');
                    if (parts.Length > 0)
                    {
                        var linkPart = parts[0].Trim().Trim('"');
                        if (linkPart.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                            s = linkPart.Substring(7);
                        else
                            s = linkPart;
                    }
                }
            }

            if (s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(7);

            s = s.Trim().Trim('<', '>');

            return s;
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
