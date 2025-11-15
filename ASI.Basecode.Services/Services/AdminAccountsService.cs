using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class AdminAccountsService : IAdminAccountsService
    {
        private readonly IStudentRepository _students;
        private readonly ITeacherRepository _teachers;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notifications;

        public AdminAccountsService(
            IStudentRepository students,
            ITeacherRepository teachers,
            IUserRepository users,
            IUnitOfWork uow,
            INotificationService notifications)
        {
            _students = students;
            _teachers = teachers;
            _users = users;
            _uow = uow;
            _notifications = notifications;
        }

        // fetch data and filters
        public async Task<AccountsFilterResult> GetStudentsAsync(AccountsFilters filters, CancellationToken ct)
        {
            var status = string.IsNullOrWhiteSpace(filters.Status) ? "Active" : filters.Status!.Trim();

            var query = _students.GetStudentsWithUser()
                .Where(s => s.User.AccountStatus == status);

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
                    UserId = s.UserId,
                    Program = s.Program,
                    YearLevel = s.YearLevel,
                    FullName = s.User.FirstName + " " + s.User.LastName,
                    IdNumber = s.User.IdNumber,
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

            return new AccountsFilterResult
            {
                Students = rows,
                Programs = programs,
                YearLevels = yearLevels,
                Filters = filters
            };
        }

        public async Task<AccountsFilterResult> GetTeachersAsync(AccountsFilters filters, CancellationToken ct)
        {
            var status = string.IsNullOrWhiteSpace(filters.Status) ? "Active" : filters.Status!.Trim();

            var query = _teachers.GetTeachersWithUser()
                .Where(t => t.User.AccountStatus == status);

            if (!string.IsNullOrWhiteSpace(filters.Name))
            {
                var name = filters.Name.Trim().ToLower();
                query = query.Where(t => (t.User.FirstName + " " + t.User.LastName).ToLower().Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(filters.Position))
            {
                var pos = filters.Position.Trim().ToLower();
                query = query.Where(t => (t.Position ?? "").ToLower().Contains(pos));
            }

            if (!string.IsNullOrWhiteSpace(filters.IdNumber))
            {
                var id = filters.IdNumber.Trim();
                query = query.Where(t => t.User.IdNumber.Contains(id));
            }

            var rows = await query
                .OrderBy(t => t.User.LastName)
                .ThenBy(t => t.User.FirstName)
                .Select(t => new TeacherListItem
                {
                    TeacherId = t.TeacherId,
                    UserId = t.UserId,
                    FullName = t.User.FirstName + " " + t.User.LastName,
                    Position = t.Position,
                    IdNumber = t.User.IdNumber
                })
                .ToListAsync(ct);

            return new AccountsFilterResult
            {
                Teachers = rows,
                Filters = filters
            };
        }

        public async Task<bool> SuspendAccount(
            int adminUserId,
            int userId,
            string status,
            string? roleLabel,
            CancellationToken ct)
        {
            var user = await _users.GetUsers()
                .FirstOrDefaultAsync(u => u.UserId == userId, ct);

            if (user == null) return false;

            user.AccountStatus = status;
            _users.UpdateUser(user);

            // Save the account status change
            await _uow.SaveChangesAsync(ct);

            // Build label for notification
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            string targetLabel = !string.IsNullOrWhiteSpace(fullName)
                ? fullName
                : $"User #{userId}";

            if (!string.IsNullOrWhiteSpace(user.IdNumber))
            {
                targetLabel = $"{targetLabel} (ID: {user.IdNumber})";
            }

            var roleText = string.IsNullOrWhiteSpace(roleLabel)
                ? "user"
                : roleLabel.Trim().ToLowerInvariant();

            // Notification: My Activity for admin
            _notifications.NotifyAdminChangedUserStatus(
                adminUserId: adminUserId,
                targetUserId: userId,
                targetLabel: targetLabel,
                roleLabel: roleText,
                newStatus: status
            );

            // Persist notification
            await _uow.SaveChangesAsync(ct);

            return true;
        }
    }
}
