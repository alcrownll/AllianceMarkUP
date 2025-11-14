using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;

namespace ASI.Basecode.Services.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IUserRepository _users;
        private readonly IUserProfileRepository _profiles;
        private readonly IStudentRepository _students;
        private readonly ITeacherRepository _teachers;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IWebRootPathAccessor _webRoot;
        private readonly INotificationService _notifications;

        public ProfileService(
            IUserRepository users,
            IUserProfileRepository profiles,
            IStudentRepository students,
            ITeacherRepository teachers,
            IHttpContextAccessor httpContext,
            IWebRootPathAccessor webRoot,
            INotificationService notifications)
        {
            _users = users;
            _profiles = profiles;
            _students = students;
            _teachers = teachers;
            _httpContext = httpContext;
            _webRoot = webRoot;
            _notifications = notifications;
        }

        public int GetCurrentUserId()
        {
            var http = _httpContext.HttpContext;
            var session = http.Session;

            var idNumber = session.GetString("IdNumber")
                ?? http.User?.FindFirst("IdNumber")?.Value;

            if (string.IsNullOrWhiteSpace(idNumber))
                throw new InvalidOperationException("No IdNumber found in session/claims. Set it at login.");

            var userId = _users.GetUsers()
                .Where(u => u.IdNumber == idNumber)
                .Select(u => u.UserId)
                .FirstOrDefault();

            if (userId == 0)
                throw new InvalidOperationException($"No User found with IdNumber '{idNumber}'.");

            // cache for future requests
            session.SetInt32("UserId", userId);
            if (session.GetString("IdNumber") == null) session.SetString("IdNumber", idNumber);

            return userId;
        }

        public int GetCurrentTeacherId()
        {
            try
            {
                // Get user ID using existing method
                var userId = GetCurrentUserId();
                if (userId <= 0)
                    return 0;

                // Get teacher ID from the teachers repository
                var teacher = _teachers.GetTeachers()
                    .FirstOrDefault(t => t.UserId == userId);

                return teacher?.TeacherId ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<StudentProfileViewModel> GetStudentProfileAsync(int userId)
        {
            var user = await _users.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            var profile = await _profiles.GetUserProfiles()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var student = await _students.GetStudents()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (user == null || student == null)
                return null;

            return new StudentProfileViewModel
            {
                // Users
                UserId = user.UserId,
                IdNumber = user.IdNumber,
                Role = user.Role,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,

                // Profiles
                ProfilePictureUrl = profile?.ProfilePictureUrl,
                MiddleName = profile?.MiddleName,
                Suffix = profile?.Suffix,
                MobileNo = profile?.MobileNo,
                HomeAddress = profile?.HomeAddress,
                Province = profile?.Province,
                Municipality = profile?.Municipality,
                Barangay = profile?.Barangay,
                DateOfBirth = profile?.DateOfBirth,
                PlaceOfBirth = profile?.PlaceOfBirth,
                Age = profile?.Age,
                MaritalStatus = profile?.MaritalStatus,
                Gender = profile?.Gender,
                Religion = profile?.Religion,
                Citizenship = profile?.Citizenship,

                // Students
                StudentId = student.StudentId,
                AdmissionTypeDb = student.AdmissionType,
                ProgramDb = student.Program,
                Department = student.Department,
                YearLevel = student.YearLevel,
                Section = student.Section
            };
        }

        // ------------------------------------------------------------
        // STUDENT PROFILE UPDATES
        // ------------------------------------------------------------

        /// <summary>
        /// User updates their own student profile (My Activity only for that user).
        /// </summary>
        public async Task UpdateStudentProfileAsync(int userId, StudentProfileViewModel input)
        {
            ApplyStudentProfileChanges(userId, input);

            // Self update -> My Activity for that user only
            _notifications.NotifyProfileUpdated(userId);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Admin updates a student's profile (My Activity for admin + Updates for student).
        /// </summary>
        public async Task UpdateStudentProfileByAdminAsync(int adminUserId, int targetUserId, StudentProfileViewModel input)
        {
            ApplyStudentProfileChanges(targetUserId, input);

            var targetDisplay = $"{input.FirstName} {input.LastName}".Trim();
            var targetIdNumber = input.IdNumber;

            _notifications.NotifyAdminUpdatedUserProfile(
                adminUserId: adminUserId,
                targetUserId: targetUserId,
                targetDisplayName: targetDisplay,
                targetIdNumber: targetIdNumber
            );

            await Task.CompletedTask;
        }

        /// <summary>
        /// Shared student update logic, no notifications.
        /// </summary>
        private void ApplyStudentProfileChanges(int userId, StudentProfileViewModel input)
        {
            TrimStringProperties(input);

            // Users
            var user = _users.GetUserById(userId);
            if (user == null) return;

            user.FirstName = input.FirstName;
            user.LastName = input.LastName;
            user.Email = input.Email;
            _users.UpdateUser(user);

            // Profile
            var profile = _profiles.GetUserProfileById(userId);
            if (profile == null)
            {
                profile = new ASI.Basecode.Data.Models.UserProfile { UserId = userId };
                MapProfile(profile, input);
                _profiles.AddUserProfile(profile);
            }
            else
            {
                MapProfile(profile, input);
                _profiles.UpdateUserProfile(profile);
            }

            // Avatar upload (only if user selected a new file)
            if (input.ProfilePhotoFile != null && input.ProfilePhotoFile.Length > 0)
            {
                var oldUrl = profile.ProfilePictureUrl;
                var newUrl = SaveAvatarFile(userId, input.ProfilePhotoFile);
                if (!string.IsNullOrWhiteSpace(newUrl))
                {
                    profile.ProfilePictureUrl = newUrl;
                    _profiles.UpdateUserProfile(profile);
                    TryDeleteOldAvatar(oldUrl);
                }
            }

            // Student
            var student = _students.GetStudents().FirstOrDefault(s => s.UserId == userId);
            if (student != null)
            {
                student.AdmissionType = input.AdmissionTypeDb;
                student.Program = input.ProgramDb;
                student.Department = input.Department;
                student.YearLevel = input.YearLevel;
                student.Section = "4A"; // temporary
                _students.UpdateStudent(student);
            }
        }

        // ------------------------------------------------------------
        // TEACHER PROFILE UPDATES
        // ------------------------------------------------------------

        public async Task<TeacherProfileViewModel> GetTeacherProfileAsync(int userId)
        {
            var user = await _users.GetUsers()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            var profile = await _profiles.GetUserProfiles()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var teacher = await _teachers.GetTeachers()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (user == null || teacher == null)
                return null;

            return new TeacherProfileViewModel
            {
                // Users
                UserId = user.UserId,
                IdNumber = user.IdNumber,
                Role = user.Role,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,

                // Profiles
                ProfilePictureUrl = profile?.ProfilePictureUrl,
                MiddleName = profile?.MiddleName,
                Suffix = profile?.Suffix,
                MobileNo = profile?.MobileNo,
                HomeAddress = profile?.HomeAddress,
                Province = profile?.Province,
                Municipality = profile?.Municipality,
                Barangay = profile?.Barangay,
                DateOfBirth = profile?.DateOfBirth,
                PlaceOfBirth = profile?.PlaceOfBirth,
                Age = profile?.Age,
                MaritalStatus = profile?.MaritalStatus,
                Gender = profile?.Gender,
                Religion = profile?.Religion,
                Citizenship = profile?.Citizenship,

                // Teachers
                TeacherId = teacher.TeacherId,
                Position = teacher.Position,
                Department = "Computer Studies"
            };
        }

        /// <summary>
        /// User updates their own teacher profile (My Activity only for that user).
        /// </summary>
        public async Task UpdateTeacherProfileAsync(int userId, TeacherProfileViewModel input)
        {
            ApplyTeacherProfileChanges(userId, input);

            _notifications.NotifyProfileUpdated(userId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Admin updates a teacher's profile (My Activity for admin + Updates for teacher).
        /// </summary>
        public async Task UpdateTeacherProfileByAdminAsync(int adminUserId, int targetUserId, TeacherProfileViewModel input)
        {
            ApplyTeacherProfileChanges(targetUserId, input);

            var targetDisplay = $"{input.FirstName} {input.LastName}".Trim();
            var targetIdNumber = input.IdNumber;

            _notifications.NotifyAdminUpdatedUserProfile(
                adminUserId: adminUserId,
                targetUserId: targetUserId,
                targetDisplayName: targetDisplay,
                targetIdNumber: targetIdNumber
            );

            await Task.CompletedTask;
        }

        /// <summary>
        /// Shared teacher update logic, no notifications.
        /// </summary>
        private void ApplyTeacherProfileChanges(int userId, TeacherProfileViewModel input)
        {
            TrimStringProperties(input);

            var user = _users.GetUserById(userId);
            if (user == null) return;

            user.FirstName = input.FirstName;
            user.LastName = input.LastName;
            user.Email = input.Email;
            _users.UpdateUser(user);

            var profile = _profiles.GetUserProfileById(userId);
            if (profile == null)
            {
                profile = new ASI.Basecode.Data.Models.UserProfile { UserId = userId };
                MapProfile(profile, input);
                _profiles.AddUserProfile(profile);
            }
            else
            {
                MapProfile(profile, input);
                _profiles.UpdateUserProfile(profile);
            }

            // Avatar upload support for teachers too
            if (input.ProfilePhotoFile != null && input.ProfilePhotoFile.Length > 0)
            {
                var oldUrl = profile.ProfilePictureUrl;
                var newUrl = SaveAvatarFile(userId, input.ProfilePhotoFile);
                if (!string.IsNullOrWhiteSpace(newUrl))
                {
                    profile.ProfilePictureUrl = newUrl;
                    _profiles.UpdateUserProfile(profile);
                    TryDeleteOldAvatar(oldUrl);
                }
            }

            var teacher = _teachers.GetTeachers().FirstOrDefault(t => t.UserId == userId);
            if (teacher != null)
            {
                teacher.Position = input.Position;
                _teachers.UpdateTeacher(teacher);
            }
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static void TrimStringProperties(object model)
        {
            if (model == null) return;
            var stringProps = model.GetType().GetProperties()
                .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(string));
            foreach (var p in stringProps)
            {
                var val = (string)p.GetValue(model);
                if (val != null) p.SetValue(model, val.Trim());
            }
        }

        private static void MapProfile(ASI.Basecode.Data.Models.UserProfile db, ProfileViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.ProfilePictureUrl)) db.ProfilePictureUrl = vm.ProfilePictureUrl;
            if (!string.IsNullOrWhiteSpace(vm.MiddleName)) db.MiddleName = vm.MiddleName;
            if (!string.IsNullOrWhiteSpace(vm.Suffix)) db.Suffix = vm.Suffix;
            if (!string.IsNullOrWhiteSpace(vm.MobileNo)) db.MobileNo = vm.MobileNo;
            if (!string.IsNullOrWhiteSpace(vm.HomeAddress)) db.HomeAddress = vm.HomeAddress;
            if (!string.IsNullOrWhiteSpace(vm.Province)) db.Province = vm.Province;
            if (!string.IsNullOrWhiteSpace(vm.Municipality)) db.Municipality = vm.Municipality;
            if (!string.IsNullOrWhiteSpace(vm.Barangay)) db.Barangay = vm.Barangay;
            if (vm.DateOfBirth.HasValue) db.DateOfBirth = vm.DateOfBirth;
            if (!string.IsNullOrWhiteSpace(vm.PlaceOfBirth)) db.PlaceOfBirth = vm.PlaceOfBirth;
            if (vm.Age.HasValue && vm.Age.Value > 0) db.Age = vm.Age.Value;
            if (!string.IsNullOrWhiteSpace(vm.MaritalStatus)) db.MaritalStatus = vm.MaritalStatus;
            if (!string.IsNullOrWhiteSpace(vm.Gender)) db.Gender = vm.Gender;
            if (!string.IsNullOrWhiteSpace(vm.Religion)) db.Religion = vm.Religion;
            if (!string.IsNullOrWhiteSpace(vm.Citizenship)) db.Citizenship = vm.Citizenship;
        }

        // ------------------------------------------------------------
        // Avatar helpers
        // ------------------------------------------------------------
        private string? SaveAvatarFile(int userId, IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            // 2 MB guard
            const long maxBytes = 2 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException("Image exceeds 2 MB.");

            // Allow-list
            var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
            var allowed = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp" };
            if (!allowed.Contains(contentType))
                throw new InvalidOperationException("Only PNG, JPG, or WEBP images are allowed.");

            // Ensure upload folder
            var uploadRoot = Path.Combine(_webRoot.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadRoot);

            // Choose extension
            var ext = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };

            // Unique filename (cache-busting)
            var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{ext}";
            var absPath = Path.Combine(uploadRoot, fileName);

            using (var fs = new FileStream(absPath, FileMode.Create))
                file.CopyTo(fs);

            // Return public web path
            return $"/uploads/avatars/{fileName}";
        }

        private void TryDeleteOldAvatar(string? webUrl)
        {
            if (string.IsNullOrWhiteSpace(webUrl)) return;
            // Expecting format: /uploads/avatars/<name>
            if (!webUrl.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase)) return;

            var absPath = Path.Combine(_webRoot.WebRootPath, webUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (File.Exists(absPath)) File.Delete(absPath);
            }
            catch
            {
                // Swallow: deleting old files is best-effort
            }
        }
    }
}
