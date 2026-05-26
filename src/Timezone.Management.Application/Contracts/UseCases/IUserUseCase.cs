using Timezone.Management.Application.Models;

namespace Timezone.Management.Application.Contracts.UseCases;

public interface IUserUseCase
{
    Task<AddUserResponse> AddUser(AddOrUpdateUserModel userRequest);

    Task<UserModel?> GetUserByUid(Guid userUid);

    Task<UpdateOrDeleteUserResponse> UpdateUser(Guid userUid, AddOrUpdateUserModel userRequest);

    Task<UpdateOrDeleteUserResponse> DeleteUser(Guid userUid);
}