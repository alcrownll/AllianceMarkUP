using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class AccountService : IAccountService
    {
        private readonly AsiBasecodeDBContext _db;

        public AccountService(AsiBasecodeDBContext db) => _db = db;

        // Produce a DateTime acceptable for "timestamp" (no tz)
        private static DateTime DbNow()
            => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        // ^ you can also just use DateTime.Now; the important part is: NOT Utc

        public async Task<(bool ok, string message)> ChangePasswordAsync(
            int userId, string oldPassword, string newPassword, CancellationToken ct = default)
        {
            if (userId <= 0) return (false, "Not signed in.");
            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
                return (false, "Please fill out all fields.");
            if (oldPassword == newPassword)
                return (false, "New password must be different from the old password.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user is null) return (false, "Account not found.");

            string currentPlain;
            try { currentPlain = PasswordManager.DecryptPassword(user.Password); }
            catch (Exception ex) { return (false, $"Unable to decrypt stored password. ({ex.Message})"); }

            if (!string.Equals(currentPlain, oldPassword))
                return (false, "Old password is incorrect.");

            try
            {
                user.Password = PasswordManager.EncryptPassword(newPassword);
                user.UpdatedAt = DbNow(); // ✅ NOT UtcNow
                await _db.SaveChangesAsync(ct);
                return (true, "Password changed successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Save failed: {ex.Message}");
            }
        }
    }
}
