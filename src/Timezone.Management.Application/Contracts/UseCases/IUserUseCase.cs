using Timezone.Management.Application.Models;
using Timezone.Management.Domain.Models;

namespace Timezone.Management.Application.Contracts.UseCases;

public interface IUserUseCase
{
    Task<AddUserResponse> AddUser(AddOrUpdateUserModel userRequest);

    Task<UserModel?> GetUserByUid(Guid userUid);
    
    Task<IEnumerable<UserModel>> GetUsers(UsersFilter filter);

    Task<UpdateOrDeleteUserResponse> UpdateUser(Guid userUid, AddOrUpdateUserModel userRequest);

    Task<UpdateOrDeleteUserResponse> DeleteUser(Guid userUid);
}