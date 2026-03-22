namespace Timezone.Management.Application.Entities;

public class User
{
    public int? Id { get; set; }

    public Guid? Guid { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}