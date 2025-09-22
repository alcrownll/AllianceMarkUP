using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

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
    }
}
