using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IUserProfileRepository
    {
        IQueryable<UserProfile> GetUserProfiles();
        UserProfile GetUserProfileById(int userId);
        void AddUserProfile(UserProfile profile);
        void UpdateUserProfile(UserProfile profile);
        void DeleteUserProfile(int userId);
    }
}