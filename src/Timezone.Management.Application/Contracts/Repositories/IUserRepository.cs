using Timezone.Management.Application.Entities;

namespace Timezone.Management.Application.Contracts.Repositories;

public interface IUserRepository
{
    Task<User> AddUser(User user);

    Task<User?> GetUserByUid(Guid userUid);

    Task UpdateUser(Guid userUid, User user);

    Task DeleteUser(Guid userUid);
}