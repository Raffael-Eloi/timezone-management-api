using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Entities;

namespace Timezone.Management.Application.Repositories;

public class UserRepository : IUserRepository
{
    public Task<User> AddUser(User user)
    {
        throw new NotImplementedException();
    }

    public Task DeleteUser(Guid userUid)
    {
        throw new NotImplementedException();
    }

    public Task<User?> GetUserByUid(Guid userUid)
    {
        throw new NotImplementedException();
    }

    public Task UpdateUser(Guid userUid, User user)
    {
        throw new NotImplementedException();
    }
}