using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Identity;

public class AppRole : IdentityRole<Guid>
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
