using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
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

        public UserService(IUserRepository repository, IMapper mapper)
        {
            _mapper = mapper;
            _repository = repository;
        }

        // ===========================
        // Authentication
        // ===========================
        public LoginResult AuthenticateUser(string idNumber, string password, ref User user)
        {
            user = null;

            try
            {
                var passwordKey = PasswordManager.EncryptPassword(password);
                user = _repository.GetUsers()
                                  .FirstOrDefault(x => x.IdNumber == idNumber && x.Password == passwordKey);

                return user != null ? LoginResult.Success : LoginResult.Failed;
            }
            catch
            {
                user = null;
                return LoginResult.Failed;
            }
        }

        // ===========================
        // Forgot / Reset Password
        // ===========================

        public User FindByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return _repository.GetUsers().FirstOrDefault(u => u.Email == email);
        }

        public PasswordResetToken CreatePasswordResetToken(User user, TimeSpan ttl)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            // Generate a URL-safe random token
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

            _repository.AddPasswordResetToken(prt); // repository saves changes
            return prt;
        }

        public User ValidateResetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var now = DateTime.UtcNow;

            // Include the user so the controller/service can access it
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
            _repository.UpdateUser(user); // repository saves changes
            return true;
        }

        public void MarkResetTokenUsed(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            _repository.MarkPasswordResetTokenUsed(token);
        }
    }
}
