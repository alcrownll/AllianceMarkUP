using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
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
    public class AdminAccountsService : IAdminAccountsService
    {
        private readonly IStudentRepository _students;
        private readonly ITeacherRepository _teachers;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _uow;

        public AdminAccountsService(
            IStudentRepository students,
            ITeacherRepository teachers,
            IUserRepository users,
            IUnitOfWork uow)
        {
            _students = students;
            _teachers = teachers;
            _users = users;
            _uow = uow;
        }

        // fetch data and filters
        public async Task<AccountsFilterResult> GetStudentsAsync(AccountsFilters filters, CancellationToken ct)
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
                    UserId = t.UserId,
                    FullName = t.User.FirstName + " " + t.User.LastName,
                    Position = t.Position
                })
                .ToListAsync(ct);

            return new AccountsFilterResult
            {
                Teachers = rows,
                Filters = filters
            };
        }

        public async Task<bool> SuspendAccount(int userId, string status, CancellationToken ct)
        {
            var user = await _users.GetUsers()
                .FirstOrDefaultAsync(u => u.UserId == userId, ct);

            if (user == null) return false;

            user.AccountStatus = status; 
            _users.UpdateUser(user);     

            await _uow.SaveChangesAsync(ct); 
            return true;
        }
    }
}
