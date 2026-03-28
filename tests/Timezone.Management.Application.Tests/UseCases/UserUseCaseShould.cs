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
    private Mock<IUserValidator> validatorMock;
    private Mock<IUserRepository> userRepositoryMock;
    private IUserUseCase userUseCase;

    [SetUp]
    public void Setup()
    {
        validatorMock = new Mock<IUserValidator>();
        userRepositoryMock = new Mock<IUserRepository>();
        userUseCase = new UserUseCase(validatorMock.Object, userRepositoryMock.Object);

        validatorMock
            .Setup(validator => validator.Validate(It.IsAny<User>()))
            .Returns(new ValidationResult());
    }

    [Test]
    public async Task GivenUser_WhenCreate_ThenTheUserShouldBeCreated()
    {
        // Arrange
        User newUser = new User
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
        User invalidUser = new User();

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

    [Test]
    public async Task GivenUserUid_WhenGetByUid_ThenTheUserShouldBeReturned()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        User existingUser = new User
        {
            Id = 1,
            Uid = userUid,
            Name = "Jack",
            Email = "jack@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync(existingUser);

        // Act
        User? response = await userUseCase.GetUserByUid(userUid);

        // Assert
        response.Should().NotBeNull();
        response!.Uid.Should().Be(userUid);
        response.Name.Should().Be("Jack");
        response.Email.Should().Be("jack@gmail.com");
    }

    [Test]
    public async Task GivenUserUid_WhenGetByUid_ThenNullShouldBeReturnedIfNotFound()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync((User?)null);

        // Act
        User? response = await userUseCase.GetUserByUid(userUid);

        // Assert
        response.Should().BeNull();
    }

    [Test]
    public async Task GivenUser_WhenUpdate_ThenTheUserShouldBeUpdated()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        User updatedUser = new User
        {
            Name = "Jack Updated",
            Email = "jack.updated@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.UpdateUser(userUid, updatedUser))
            .Returns(Task.CompletedTask);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, updatedUser);

        // Assert
        response.IsValid.Should().BeTrue();

        userRepositoryMock
            .Verify(repo => repo.UpdateUser(userUid, updatedUser),
            Times.Once);
    }

    [Test]
    public async Task GivenUser_WhenUpdate_ThenTheUserShouldBeValidated()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        User invalidUser = new User();

        ValidationFailure error = new("Name", "Name", "Name is required.");

        validatorMock
            .Setup(validator => validator.Validate(invalidUser))
            .Returns(new ValidationResult
            {
                Errors = [error]
            });

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, invalidUser);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Errors.First().Should().Be(error);

        userRepositoryMock
            .Verify(repo => repo.UpdateUser(It.IsAny<Guid>(), It.IsAny<User>()),
            Times.Never);
    }

    [Test]
    public async Task GivenUserUid_WhenDelete_ThenTheUserShouldBeDeleted()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync(new User { Uid = userUid });

        userRepositoryMock
            .Setup(repo => repo.DeleteUser(userUid))
            .Returns(Task.CompletedTask);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.DeleteUser(userUid);

        // Assert
        response.IsValid.Should().BeTrue();

        userRepositoryMock
            .Verify(repo => repo.DeleteUser(userUid),
            Times.Once);
    }

    [Test]
    public async Task GivenUserUid_WhenDelete_ThenTheUserShouldExist()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync((User?)null);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.DeleteUser(userUid);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Errors.First().ErrorMessage.Should().Be("User not found.");

        userRepositoryMock
            .Verify(repo => repo.DeleteUser(It.IsAny<Guid>()),
            Times.Never);
    }
}