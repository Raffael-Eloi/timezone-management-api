using Timezone.Management.Application.Entities;

namespace Timezone.Management.Application.Contracts.Repositories;

public interface IUserRepository
{
    Task<User> AddUser(User user);
}