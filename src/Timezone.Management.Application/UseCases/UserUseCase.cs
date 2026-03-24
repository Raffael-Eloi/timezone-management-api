using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;

namespace Timezone.Management.Application.UseCases;

public class UserUseCase(IUserRepository userRepository) : IUserUseCase
{
    public async Task<AddUserResponse> AddUser(User user)
    {
        User addedUser = await userRepository.AddUser(user);

        return new AddUserResponse
        {
            UserId = addedUser.Uid
        };
    }
}