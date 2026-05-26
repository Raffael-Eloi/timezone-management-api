using FluentValidation.Results;
using Timezone.Management.Domain.Entities;

namespace Timezone.Management.Application.Contracts.Validators;

public interface IUserValidator
{
    ValidationResult Validate(User user);
}