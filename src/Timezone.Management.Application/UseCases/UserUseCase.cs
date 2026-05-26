using FluentValidation.Results;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Models;
using Timezone.Management.Domain.Contracts.Repositories;
using Timezone.Management.Domain.Entities;
using Timezone.Management.Domain.Models;

namespace Timezone.Management.Application.UseCases;

public class UserUseCase(IUserValidator validator, IUserRepository repository) : IUserUseCase
{
    public async Task<AddUserResponse> AddUser(AddOrUpdateUserModel userRequest)
    {
	    IEnumerable<User> existingUsersWithEmail = await repository.GetUsers(new UsersFilter{Email = userRequest.Email});
	    if (existingUsersWithEmail.Any())
		    return new AddUserResponse { Errors = [new ValidationFailure("Email", "Email already exists.")] };
	    
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

	public async Task<IEnumerable<UserModel>> GetUsers(UsersFilter filter)
	{
		IEnumerable<User> users = await repository.GetUsers(filter);
		return users.Select(MapUserModel);
	}

	private static UserModel MapUserModel(User user) => new() { Uid = user.Uid, Name = user.Name, Email = user.Email };

    public async Task<UpdateOrDeleteUserResponse> UpdateUser(Guid userUid, AddOrUpdateUserModel userRequest)
    {
	    User? existingUser = await repository.GetUserByUid(userUid);
	    if (existingUser is null)
		    return new UpdateOrDeleteUserResponse { Errors = [new ValidationFailure("UserUid", "User not found.")] };

	    List<User> usersWithEmail = [.. await repository.GetUsers(new UsersFilter{Email = userRequest.Email})];
	    if (usersWithEmail.Count >= 1 && usersWithEmail.First().Uid != userUid)
		    return new UpdateOrDeleteUserResponse { Errors = [new ValidationFailure("Email", "Email already exists.")] };
	    
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
        {
            return new UpdateOrDeleteUserResponse
            {
                Errors = [new ValidationFailure("UserUid", "User not found.")]
            };
        }

        await repository.DeleteUser(userUid);

        return new UpdateOrDeleteUserResponse();
    }
}