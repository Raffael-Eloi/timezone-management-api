using FluentValidation.Results;
using Timezone.Management.Application.Entities;

namespace Timezone.Management.Application.Contracts.Validators;

public interface IUserValidator
{
    ValidationResult Validate(User user);
}