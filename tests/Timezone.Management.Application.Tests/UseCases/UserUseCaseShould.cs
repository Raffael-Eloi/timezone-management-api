using FluentAssertions;
using Moq;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;
using Timezone.Management.Application.UseCases;

namespace Timezone.Management.Application.Tests.UseCases;

internal class UserUseCaseShould
{
    [Test]
    public async Task GivenUser_WhenCreate_ThenTheUserShouldBeCreated()
    {
        // Arrange

        var newUser = new User
        {
            Name = "Jack",
            Email = "jack@gmail.com"
        };

        int userId = 1;

        Guid userGuid = Guid.NewGuid();

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repo => repo.AddUser(newUser))
            .Callback(() =>
            {
                newUser.Id = userId;
                newUser.Uid = userGuid;
            })
            .ReturnsAsync(newUser);

        IUserUseCase userUseCase = new UserUseCase(userRepositoryMock.Object);

        // Act

        AddUserResponse response = await userUseCase.AddUser(newUser);

        // Assert
        response.UserId.Should().Be(userGuid);

        newUser.Id.Should().Be(userId);
        newUser.Uid.Should().Be(userGuid);
    }
}