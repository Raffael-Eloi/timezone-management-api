using FluentValidation.Results;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Entities;

namespace Timezone.Management.Application.Validators;

public class UserValidator : IUserValidator
{
    public ValidationResult Validate(User user)
    {
        throw new NotImplementedException();
    }
}