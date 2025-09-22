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
    public class UserProfileRepository : BaseRepository, IUserProfileRepository
    {
        public UserProfileRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<UserProfile> GetUserProfiles() =>
            this.GetDbSet<UserProfile>();

        public UserProfile GetUserProfileById(int userId) =>
            this.GetDbSet<UserProfile>().FirstOrDefault(x => x.UserId == userId);

        public void AddUserProfile(UserProfile profile)
        {
            this.GetDbSet<UserProfile>().Add(profile);
            UnitOfWork.SaveChanges();
        }

        public void UpdateUserProfile(UserProfile profile)
        {
            this.GetDbSet<UserProfile>().Update(profile);
            UnitOfWork.SaveChanges();
        }

        public void DeleteUserProfile(int userId)
        {
            var profile = GetUserProfileById(userId);
            if (profile != null)
            {
                this.GetDbSet<UserProfile>().Remove(profile);
                UnitOfWork.SaveChanges();
            }
        }
    }
}
