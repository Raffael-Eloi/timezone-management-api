using FluentAssertions;
using FluentValidation.Results;
using Moq;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;
using Timezone.Management.Application.UseCases;

namespace Timezone.Management.Application.Tests.UseCases;

internal class UserUseCaseShould
{
    private Mock<IUserRepository> userRepositoryMock;
    private Mock<IUserValidator> validatorMock;
    private IUserUseCase userUseCase;

    [SetUp]
    public void Setup()
    {
        userRepositoryMock = new Mock<IUserRepository>();
        validatorMock = new Mock<IUserValidator>();
        userUseCase = new UserUseCase(userRepositoryMock.Object);
    }

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

        userRepositoryMock
            .Setup(repo => repo.AddUser(newUser))
            .Callback(() =>
            {
                newUser.Id = userId;
                newUser.Uid = userGuid;
            })
            .ReturnsAsync(newUser);

        // Act
        AddUserResponse response = await userUseCase.AddUser(newUser);

        // Assert
        response.UserUid.Should().Be(userGuid);

        newUser.Id.Should().Be(userId);
        newUser.Uid.Should().Be(userGuid);
    }

    [Test]
    public async Task GivenUser_WhenCreate_ThenTheUserShouldBeValidated()
    {
        // Arrange
        var invalidUser = new User();

        ValidationFailure error = new("Name", "Name", "Name is required.");

        validatorMock
            .Setup(validator => validator.Validate(invalidUser))
            .Returns(new ValidationResult
            {
                Errors = [error]
            });

        // Act
        AddUserResponse response = await userUseCase.AddUser(invalidUser);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Errors.First().Should().Be(error);

        userRepositoryMock
            .Verify(repo => repo.AddUser(It.IsAny<User>()), 
            Times.Never);
    }
}