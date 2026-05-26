using FluentAssertions;
using FluentValidation.Results;
using Moq;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Domain.Entities;
using Timezone.Management.Application.Models;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Domain.Models;

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

        userRepositoryMock
            .Setup(repo => repo.GetUsers(It.IsAny<UsersFilter>()))
            .ReturnsAsync([]);
    }

    [Test]
    public async Task GivenUser_WhenCreate_ThenTheUserShouldBeCreated()
    {
        // Arrange
        AddOrUpdateUserModel newUser = new()
        {
            Name = "Jack",
            Email = "jack@gmail.com"
        };

        Guid addedUserUid = Guid.NewGuid();

        userRepositoryMock
            .Setup(repo => repo.AddUser(It.Is<User>(user => user.Name == newUser.Name &&  user.Email == newUser.Email)))
            .ReturnsAsync(addedUserUid);

        // Act
        AddUserResponse response = await userUseCase.AddUser(newUser);

        // Assert
        response.UserUid.Should().Be(addedUserUid);
    }

    [Test]
    public async Task GivenUser_WhenCreate_ThenTheUserShouldBeValidated()
    {
        // Arrange
        AddOrUpdateUserModel invalidUser = new();

        ValidationFailure error = new("Name", "Name", "Name is required.");

        validatorMock
            .Setup(validator => validator.Validate(It.IsAny<User>()))
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
        UserModel? response = await userUseCase.GetUserByUid(userUid);

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
        UserModel? response = await userUseCase.GetUserByUid(userUid);

        // Assert
        response.Should().BeNull();
    }
    
    [Test]
    public async Task GivenFilter_WhenGetUsersWithFilter_ThenTheUsersShouldBeReturned()
    {
	    // Arrange
	    UsersFilter filter = new();

	    User existingUser1 = new()
	    {
		    Uid = Guid.NewGuid(),
	    };
	    
	    User existingUser2 = new()
	    {
		    Uid = Guid.NewGuid(),
	    };

	    userRepositoryMock
		    .Setup(repo => repo.GetUsers(filter))
		    .ReturnsAsync([existingUser1, existingUser2]);

	    // Act
	    IEnumerable<UserModel> response = await userUseCase.GetUsers(filter);

	    // Assert
	    response.Should().NotBeNullOrEmpty();
	    response.Count().Should().Be(2);
	    response!.First().Uid.Should().Be(existingUser1.Uid);
	    response!.Last().Uid.Should().Be(existingUser2.Uid);
    }

    [Test]
    public async Task GivenUser_WhenUpdate_ThenTheUserShouldBeUpdated()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        AddOrUpdateUserModel updatedUser = new()
        {
            Name = "Jack Updated",
            Email = "jack.updated@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync(new User { Uid = userUid });

        userRepositoryMock
            .Setup(repo => repo.UpdateUser(userUid, It.Is<User>(user => user.Name == updatedUser.Name &&  user.Email == updatedUser.Email)))
            .Returns(Task.CompletedTask);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, updatedUser);

        // Assert
        response.IsValid.Should().BeTrue();

        userRepositoryMock
            .Verify(repo => repo.UpdateUser(userUid, It.IsAny<User>()),
            Times.Once);
    }

    [Test]
    public async Task GivenUser_WhenUpdate_ThenTheUserShouldBeValidated()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        AddOrUpdateUserModel invalidUser = new AddOrUpdateUserModel();

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync(new User { Uid = userUid });

        ValidationFailure error = new("Name", "Name", "Name is required.");

        validatorMock
            .Setup(validator => validator.Validate(It.IsAny<User>()))
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

    [Test]
    public async Task GivenUserWithDuplicateEmail_WhenCreate_ThenEmailAlreadyExistsErrorShouldBeReturned()
    {
        // Arrange
        AddOrUpdateUserModel newUser = new()
        {
            Name = "Jack",
            Email = "jack@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.GetUsers(It.Is<UsersFilter>(f => f.Email == newUser.Email)))
            .ReturnsAsync([new User { Email = newUser.Email }]);

        // Act
        AddUserResponse response = await userUseCase.AddUser(newUser);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Errors.First().ErrorMessage.Should().Be("Email already exists.");

        userRepositoryMock
            .Verify(repo => repo.AddUser(It.IsAny<User>()),
            Times.Never);
    }

    [Test]
    public async Task GivenNonExistentUserUid_WhenUpdate_ThenNotFoundErrorShouldBeReturned()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        AddOrUpdateUserModel updatedUser = new()
        {
            Name = "Jack",
            Email = "jack@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync((User?)null);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, updatedUser);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Errors.First().ErrorMessage.Should().Be("User not found.");

        userRepositoryMock
            .Verify(repo => repo.UpdateUser(It.IsAny<Guid>(), It.IsAny<User>()),
            Times.Never);
    }

    [Test]
    public async Task GivenUserWithEmailTakenByAnotherUser_WhenUpdate_ThenEmailAlreadyExistsErrorShouldBeReturned()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        AddOrUpdateUserModel updatedUser = new()
        {
            Name = "Jack",
            Email = "taken@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync(new User { Uid = userUid });

        userRepositoryMock
            .Setup(repo => repo.GetUsers(It.Is<UsersFilter>(f => f.Email == updatedUser.Email)))
            .ReturnsAsync([new User { Uid = Guid.NewGuid(), Email = updatedUser.Email }]);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, updatedUser);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Errors.First().ErrorMessage.Should().Be("Email already exists.");

        userRepositoryMock
            .Verify(repo => repo.UpdateUser(It.IsAny<Guid>(), It.IsAny<User>()),
            Times.Never);
    }

    [Test]
    public async Task GivenUserWithTheirOwnEmail_WhenUpdate_ThenUpdateShouldSucceed()
    {
        // Arrange
        Guid userUid = Guid.NewGuid();

        AddOrUpdateUserModel updatedUser = new()
        {
            Name = "Jack Updated",
            Email = "jack@gmail.com"
        };

        userRepositoryMock
            .Setup(repo => repo.GetUserByUid(userUid))
            .ReturnsAsync(new User { Uid = userUid });

        userRepositoryMock
            .Setup(repo => repo.GetUsers(It.Is<UsersFilter>(f => f.Email == updatedUser.Email)))
            .ReturnsAsync([new User { Uid = userUid, Email = updatedUser.Email }]);

        userRepositoryMock
            .Setup(repo => repo.UpdateUser(userUid, It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, updatedUser);

        // Assert
        response.IsValid.Should().BeTrue();

        userRepositoryMock
            .Verify(repo => repo.UpdateUser(userUid, It.IsAny<User>()),
            Times.Once);
    }
}