using Tom.WebApi.Api.Identity;
using Tom.WebApi.Api.Infrastructure;

namespace Tom.WebApi.Api.Notes;

// Example feature. Delete this folder and the AddNotesModule / MapNotesModule calls in Program.cs to remove it.
public static class NotesModule
{
    public static IServiceCollection AddNotesModule(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddScoped<NoteService>();
        services.AddAuthorizationBuilder()
            .AddPermission(NotesPermissions.View, AppRoles.Admin)
            .AddPermission(NotesPermissions.Manage, AppRoles.Admin);
        return services;
    }

    public static IEndpointRouteBuilder MapNotesModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapNoteApi();
        return endpoints;
    }
}
