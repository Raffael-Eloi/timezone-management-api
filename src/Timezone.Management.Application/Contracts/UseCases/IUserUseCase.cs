using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;

namespace Timezone.Management.Application.Contracts.UseCases;

public interface IUserUseCase
{
    Task<AddUserResponse> AddUser(User user);
}