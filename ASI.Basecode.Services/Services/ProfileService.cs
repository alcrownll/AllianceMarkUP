using System;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IUserRepository _users;
        private readonly IUserProfileRepository _profiles;
        private readonly IStudentRepository _students;
        private readonly ITeacherRepository _teachers;
        private readonly IHttpContextAccessor _httpContext;

        public ProfileService(
            IUserRepository users,
            IUserProfileRepository profiles,
            IStudentRepository students,
            ITeacherRepository teachers,
            IHttpContextAccessor httpContext)
        {
            _users = users;
            _profiles = profiles;
            _students = students;
            _teachers = teachers;
            _httpContext = httpContext;
        }

        // ------------------------------------------------------------
        // Current user via Session ("IdNumber") + Student table
        // ------------------------------------------------------------
        public int GetCurrentUserId()
        {
            // Prefer cached UserId in session if you set it at login
            var cachedUserId = _httpContext.HttpContext?.Session?.GetInt32("UserId");
            if (cachedUserId.HasValue && cachedUserId.Value > 0)
                return cachedUserId.Value;

            var idNumber = _httpContext.HttpContext?.Session?.GetString("IdNumber");
            if (string.IsNullOrWhiteSpace(idNumber))
                throw new InvalidOperationException("No IdNumber found in session. Make sure to set it at login.");

            // Use existing repository functions only (no new repo methods)
            var userId = _students.GetStudentsWithUser() // includes User via .Include(s => s.User)
                .Where(s => s.User.IdNumber == idNumber)
                .Select(s => s.UserId)
                .FirstOrDefault();

            if (userId == 0)
                throw new InvalidOperationException($"No Student linked to IdNumber '{idNumber}'.");

            // Optionally cache for future requests
            _httpContext.HttpContext?.Session?.SetInt32("UserId", userId);
            return userId;
        }

        // ------------------------------------------------------------
        // STUDENT
        // ------------------------------------------------------------
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
                StudentStatus = "Enrolled"
            };
        }

        public async Task UpdateStudentProfileAsync(int userId, StudentProfileViewModel input)
        {
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

            // Student
            var student = _students.GetStudents().FirstOrDefault(s => s.UserId == userId);
            if (student != null)
            {
                student.AdmissionType = input.AdmissionTypeDb; 
                student.Program = input.ProgramDb;
                student.Department = input.Department;
                student.YearLevel = input.YearLevel;
                student.StudentStatus = "Enrolled";
                _students.UpdateStudent(student);
            }

            await Task.CompletedTask;
        }

        // ------------------------------------------------------------
        // TEACHER (kept for completeness)
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
                Department = "Computer Studies" // temporary
            };
        }

        public async Task UpdateTeacherProfileAsync(int userId, TeacherProfileViewModel input)
        {
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

            var teacher = _teachers.GetTeachers().FirstOrDefault(t => t.UserId == userId);
            if (teacher != null)
            {
                teacher.Position = input.Position;
                _teachers.UpdateTeacher(teacher);
            }

            await Task.CompletedTask;
        }

        // ------------------------------------------------------------
        // Helper
        // ------------------------------------------------------------
        private static void MapProfile(ASI.Basecode.Data.Models.UserProfile db, ProfileViewModel vm)
        {
            db.ProfilePictureUrl = vm.ProfilePictureUrl;
            db.MiddleName = vm.MiddleName;
            db.Suffix = vm.Suffix;
            db.MobileNo = vm.MobileNo;
            db.HomeAddress = vm.HomeAddress;
            db.Province = vm.Province;
            db.Municipality = vm.Municipality;
            db.Barangay = vm.Barangay;
            db.DateOfBirth = vm.DateOfBirth;
            db.PlaceOfBirth = vm.PlaceOfBirth;
            db.Age = vm.Age ?? 0;
            db.MaritalStatus = vm.MaritalStatus;
            db.Gender = vm.Gender;
            db.Religion = vm.Religion;
            db.Citizenship = vm.Citizenship;
        }
    }
}
