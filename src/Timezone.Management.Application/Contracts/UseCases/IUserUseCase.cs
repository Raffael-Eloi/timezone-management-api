using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;

namespace Timezone.Management.Application.Contracts.UseCases;

public interface IUserUseCase
{
    Task<AddUserResponse> AddUser(User user);

    Task<User?> GetUserByUid(Guid userUid);

    Task<UpdateOrDeleteUserResponse> UpdateUser(Guid userUid, User user);

    Task<UpdateOrDeleteUserResponse> DeleteUser(Guid userUid);
}