using FluentValidation.Results;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;

namespace Timezone.Management.Application.UseCases;

public class UserUseCase(IUserValidator validator, IUserRepository repository) : IUserUseCase
{
    public async Task<AddUserResponse> AddUser(User user)
    {
        ValidationResult validationResult = validator.Validate(user);

        if (!validationResult.IsValid)
            return new AddUserResponse { Errors = validationResult.Errors };

        User addedUser = await repository.AddUser(user);

        return new AddUserResponse
        {
            UserUid = addedUser.Uid
        };
    }

    public async Task<User?> GetUserByUid(Guid userUid) =>
        await repository.GetUserByUid(userUid);

    public async Task<UpdateOrDeleteUserResponse> UpdateUser(Guid userUid, User user)
    {
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