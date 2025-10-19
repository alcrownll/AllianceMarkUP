using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using static ASI.Basecode.Resources.Constants.Enums;
using System.Threading.Tasks;
using System;

namespace ASI.Basecode.Services.Interfaces
{
    public enum ForgotPasswordResult
    {
        Success,
        NotFound,
        RateLimited,
        Error
    }

    public interface IUserService
    {
        LoginResult AuthenticateUser(string userid, string password, ref User user);

        // Forgot / reset password
        User FindByEmail(string email);
        PasswordResetToken CreatePasswordResetToken(User user, TimeSpan ttl);
        User ValidateResetToken(string token);
        bool ResetPassword(int userId, string newPassword);
        void MarkResetTokenUsed(string token);

        // UPDATED: controller supplies exact URL via delegate
        Task<ForgotPasswordResult> RequestPasswordResetAsync(
            string email,
            TimeSpan ttl,
            Func<string, string> linkFactory);
    }
}
