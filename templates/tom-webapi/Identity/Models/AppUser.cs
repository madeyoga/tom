using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }
}
