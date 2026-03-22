using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;

namespace Timezone.Management.Application.UseCases;

public class UserUseCase : IUserUseCase
{
    public Task<AddUserResponse> AddUser(User user)
    {
        throw new NotImplementedException();
    }
}