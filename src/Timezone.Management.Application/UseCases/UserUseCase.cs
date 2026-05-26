using FluentValidation.Results;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Models;
using Timezone.Management.Domain.Entities;

namespace Timezone.Management.Application.UseCases;

public class UserUseCase(IUserValidator validator, IUserRepository repository) : IUserUseCase
{
    public async Task<AddUserResponse> AddUser(AddOrUpdateUserModel userRequest)
    {
	    User user = MapUser(userRequest);
	    
        ValidationResult validationResult = validator.Validate(user);

        if (!validationResult.IsValid)
            return new AddUserResponse { Errors = validationResult.Errors };

        Guid userId = await repository.AddUser(user);

        return new AddUserResponse
        {
            UserUid = userId
		};
    }

	private static User MapUser(AddOrUpdateUserModel userRequest) => new() { Name = userRequest.Name, Email = userRequest.Email };

	public async Task<UserModel?> GetUserByUid(Guid userUid)
    {
        User? user = await repository.GetUserByUid(userUid);
        
        if (user is null)
	        return null;
        
        return MapUserModel(user);
    }

    private static UserModel MapUserModel(User user) => new() { Uid = user.Uid, Name = user.Name, Email = user.Email };

    public async Task<UpdateOrDeleteUserResponse> UpdateUser(Guid userUid, AddOrUpdateUserModel userRequest)
    {
	    User user = MapUser(userRequest);
	    
        ValidationResult validationResult = validator.Validate(user);

        if (!validationResult.IsValid)
            return new UpdateOrDeleteUserResponse { Errors = validationResult.Errors };

        await repository.UpdateUser(userUid, user);

        return new UpdateOrDeleteUserResponse();
    }

    public async Task<UpdateOrDeleteUserResponse> DeleteUser(Guid userUid)
    {
        User? user = await repository.GetUserByUid(userUid);

        if (user is null)
            return new UpdateOrDeleteUserResponse
            {
                Errors = [new ValidationFailure("UserUid", "User not found.")]
            };

        await repository.DeleteUser(userUid);

        return new UpdateOrDeleteUserResponse();
    }
}