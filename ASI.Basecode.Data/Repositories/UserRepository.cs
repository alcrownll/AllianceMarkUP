using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        // ========= Users =========
        public IQueryable<User> GetUsers() => this.GetDbSet<User>();

        public User GetUserById(int userId) =>
            this.GetDbSet<User>().FirstOrDefault(x => x.UserId == userId);

        public bool UserExists(int userId) =>
            this.GetDbSet<User>().Any(x => x.UserId == userId);

        public void AddUser(User user)
        {
            this.GetDbSet<User>().Add(user);
            UnitOfWork.SaveChanges();
        }

        public void UpdateUser(User user)
        {
            this.GetDbSet<User>().Update(user);
            UnitOfWork.SaveChanges();
        }

        public void DeleteUser(int userId)
        {
            var user = GetUserById(userId);
            if (user != null)
            {
                this.GetDbSet<User>().Remove(user);
                UnitOfWork.SaveChanges();
            }
        }

        // ========= Password Reset Tokens =========
        public IQueryable<PasswordResetToken> PasswordResetTokens()
            => this.GetDbSet<PasswordResetToken>();

        public void AddPasswordResetToken(PasswordResetToken token)
        {
            this.GetDbSet<PasswordResetToken>().Add(token);
            UnitOfWork.SaveChanges();
        }

        public void MarkPasswordResetTokenUsed(string token)
        {
            var prt = this.GetDbSet<PasswordResetToken>().FirstOrDefault(t => t.Token == token);
            if (prt != null && prt.UsedAt == null)
            {
                prt.UsedAt = DateTime.UtcNow;
                UnitOfWork.SaveChanges();
            }
        }

        public void SaveChanges() => UnitOfWork.SaveChanges();
    }
}
