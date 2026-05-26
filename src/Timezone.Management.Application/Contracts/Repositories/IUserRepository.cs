using Timezone.Management.Domain.Entities;
using Timezone.Management.Domain.Models;

namespace Timezone.Management.Application.Contracts.Repositories;

public interface IUserRepository
{
    Task<Guid> AddUser(User user);

    Task<User?> GetUserByUid(Guid userUid);
    
    Task<IEnumerable<User>> GetUsers(UsersFilter filter);

    Task UpdateUser(Guid userUid, User user);

    Task DeleteUser(Guid userUid);
}