using FluentAssertions;
using FluentValidation.Results;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Validators;

namespace Timezone.Management.Application.Tests.Validators;

internal class UserValidatorShould
{
    private IUserValidator userValidator;
    private User user;

    [SetUp]
    public void Setup()
    {
        userValidator = new UserValidator();

        user = new User();
    }

    [Test]
    public void GivenUser_WhenNameIsEmpty_ThenShouldReturnValidationError()
    {
        // Arrange
        user.Name = string.Empty;

        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Name) && x.ErrorMessage == $"'{nameof(user.Name)}' must not be empty.");
    }

    [Test]
    public void GivenUser_WhenNameHasLessThan3Characters_ThenShouldReturnValidationError()
    {
        // Arrange
        user.Name = "ab";

        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Name) && x.ErrorMessage == $"The length of '{nameof(user.Name)}' must be at least 3 characters. You entered 2 characters.");
    }

    [Test]
    public void GivenUser_WhenNameHasMoreThan100Characters_ThenShouldReturnValidationError()
    {
        // Arrange
        user.Name = new string('a', 101);

        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Name) && x.ErrorMessage == $"The length of '{nameof(user.Name)}' must be 100 characters or fewer. You entered 101 characters.");
    }

    [Test]
    public void GivenUser_WhenEmailIsEmpty_ThenShouldReturnValidationError()
    {
        // Arrange
        user.Email = string.Empty;

        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Email) && x.ErrorMessage == $"'{nameof(user.Email)}' must not be empty.");
    }

    [Test]
    public void GivenUser_WhenEmailHasLessThan3Characters_ThenShouldReturnValidationError()
    {
        // Arrange
        user.Email = "ab";

        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Email) && x.ErrorMessage == $"The length of '{nameof(user.Email)}' must be at least 3 characters. You entered 2 characters.");
    }

    [Test]
    public void GivenUser_WhenEmailHasMoreThan100Characters_ThenShouldReturnValidationError()
    {
        // Arrange
        user.Email = new string('a', 101);

        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Email) && x.ErrorMessage == $"The length of '{nameof(user.Email)}' must be 100 characters or fewer. You entered 101 characters.");
    }
}