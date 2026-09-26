using System.Security.Claims;

namespace Tom.WebApi.Api.Shared;

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public Guid? UserId => Guid.TryParse(
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id)
        ? id
        : null;

    public Guid RequiredUserId => UserId ?? throw new InvalidOperationException("No authenticated user.");
}
