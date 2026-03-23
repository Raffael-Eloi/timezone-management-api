using FluentAssertions;
using FluentValidation.Results;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Validators;

namespace Timezone.Management.Application.Tests.Validators;

internal class UserValidatorShould
{
    [Test]
    public void GivenUser_WhenNameIsEmpty_ThenShouldReturnValidationError()
    {
        // Arrange
        var user = new User
        {
            Name = string.Empty,
        };

        IUserValidator userValidator = new UserValidator();
        
        // Act
        ValidationResult result = userValidator.Validate(user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(user.Name) && x.ErrorMessage == $"'{nameof(user.Name)}' must not be empty.");
    } 
}