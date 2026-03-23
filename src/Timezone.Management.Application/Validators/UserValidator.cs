using FluentValidation;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.Entities;

namespace Timezone.Management.Application.Validators;

public class UserValidator : AbstractValidator<User>, IUserValidator
{
    public UserValidator()
    {
        RuleFor(user => user.Name)
            .NotEmpty();
    }
}