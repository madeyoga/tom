using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Api.Identity;

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
        Id = Guid.CreateVersion7();
    }

    public AppRole(string roleName)
        : this()
    {
        Name = roleName;
    }
}
