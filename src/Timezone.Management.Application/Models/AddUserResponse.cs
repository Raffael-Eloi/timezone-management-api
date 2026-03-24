using FluentValidation.Results;

namespace Timezone.Management.Application.Models;

public class AddUserResponse : ValidationResult
{
    public Guid? UserUid { get; set; }
}