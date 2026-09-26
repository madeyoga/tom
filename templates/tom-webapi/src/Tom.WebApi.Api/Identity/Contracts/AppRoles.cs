using System.Collections.Immutable;

namespace Tom.WebApi.Api.Identity.Contracts;

public static class AppRoles
{
    public const string Admin = "Admin";

    public static readonly ImmutableArray<string> All = [Admin];
}
