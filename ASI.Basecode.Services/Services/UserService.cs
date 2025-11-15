using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Security.Cryptography;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly IMapper _mapper;
        private readonly IEmailSender _email;

        public UserService(IUserRepository repository, IMapper mapper, IEmailSender email)
        {
            _mapper = mapper;
            _repository = repository;
            _email = email;
        }

        public LoginResult AuthenticateUser(string idNumber, string password, ref User user)
        {
            user = null;

            try
            {
                user = _repository.GetUsers()
                                  .FirstOrDefault(x => x.IdNumber == idNumber);

                if (user == null)
                    return LoginResult.Failed;

                if (string.Equals(user.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
                    return LoginResult.Inactive;

                var passwordKey = PasswordManager.EncryptPassword(password);
                if (user.Password != passwordKey)
                    return LoginResult.Failed;

                return LoginResult.Success;
            }
            catch
            {
                user = null;
                return LoginResult.Failed;
            }
        }

        public User FindByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return _repository.GetUsers().FirstOrDefault(u => u.Email == email);
        }

        public PasswordResetToken CreatePasswordResetToken(User user, TimeSpan ttl)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var raw = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(raw)
                               .Replace('+', '-')
                               .Replace('/', '_')
                               .TrimEnd('=');

            var prt = new PasswordResetToken
            {
                UserId = user.UserId,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl)
            };

            _repository.AddPasswordResetToken(prt);
            return prt;
        }

        public User ValidateResetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var now = DateTime.UtcNow;

            var prt = _repository.PasswordResetTokens()
                                 .Include(t => t.User)
                                 .FirstOrDefault(t => t.Token == token &&
                                                      t.UsedAt == null &&
                                                      t.ExpiresAt > now);

            return prt?.User;
        }

        public bool ResetPassword(int userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword)) return false;

            var user = _repository.GetUsers().FirstOrDefault(u => u.UserId == userId);
            if (user == null) return false;

            user.Password = PasswordManager.EncryptPassword(newPassword);
            _repository.UpdateUser(user);
            return true;
        }

        public void MarkResetTokenUsed(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            _repository.MarkPasswordResetTokenUsed(token);
        }

        public async Task<ForgotPasswordResult> RequestPasswordResetAsync(
    string email,
    TimeSpan ttl,
    Func<string, string> linkFactory)
        {
            var user = FindByEmail(email);
            if (user == null) return ForgotPasswordResult.NotFound;

            var token = CreatePasswordResetToken(user, ttl);

            var link = linkFactory(token.Token);

            var html = $@"
        <p>Use the link below to reset your password. It expires in {(int)ttl.TotalMinutes} minutes.</p>
        <p><a href=""{link}"">{link}</a></p>";

            await _email.SendAsync(user.Email, "Password Reset", html);
            return ForgotPasswordResult.Success;
        }
    }
}
