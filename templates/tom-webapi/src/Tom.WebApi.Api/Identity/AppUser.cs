using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Api.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }
}
