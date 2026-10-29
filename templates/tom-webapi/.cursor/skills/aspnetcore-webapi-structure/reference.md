# Reference: example `Notes` feature

A small feature that shows every SKILL.md rule once. Copy the shape, not the names. Snippets are trimmed. `{App}` is the project name.

## File map

```
src/{App}.Api/
├── Program.cs
├── BannedSymbols.txt
├── Infrastructure/
│   ├── InfrastructureSetup.cs        # AddInfrastructure
│   ├── DbTransactions.cs             # InTransactionAsync helper
│   ├── Auth/PermissionPolicies.cs
│   └── Seeding/DatabaseInitializer.cs
├── Shared/
│   ├── CurrentUser.cs
│   ├── IDataSeeder.cs
│   ├── GuidIds.cs                    # IGuidId + GuidIdJsonConverter<TId>
│   ├── Pagination.cs                 # PaginatedItems<T>, PaginationRequest
│   ├── Problems.cs                   # TypedResults.Problem helpers
│   └── Result.cs                     # Result<T> / Result for services
├── Data/
│   ├── AppDbContext.cs
│   ├── AppUser.cs, AppRole.cs        # Identity entities
│   ├── Note.cs                       # NoteId, entity, NoteConfiguration
│   └── Migrations/
├── Identity/
│   ├── IdentityModule.cs
│   ├── AppRoles.cs
│   ├── RoleSeeder.cs
│   └── UsersApi.cs
├── Notes/
│   ├── NotesModule.cs
│   ├── NotesPermissions.cs
│   ├── NoteApi.cs                    # endpoints + request/response records
│   └── NoteService.cs                # writes other features need
└── Reports/
    ├── ReportsModule.cs
    └── ActivityReportApi.cs          # read-only, across features
tests/{App}.Api.Tests/
├── ApiFactory.cs                     # WebApplicationFactory + Testcontainers
├── Notes/NoteApiTests.cs
└── Architecture/*.cs                 # see enforceable-checks.md
```

## Entity and configuration

`Data/Note.cs`: the typed id, the entity, and its configuration in one file named after the entity (DATA-02). Move `NoteId` to its own file when a second entity stores one. `AppDbContext` exposes `public DbSet<Note> Notes => Set<Note>();` (DATA-03). The converter type is `Shared/GuidIds.cs` (below).

```csharp
namespace {App}.Api.Data;

[JsonConverter(typeof(GuidIdJsonConverter<NoteId>))]
public readonly record struct NoteId(Guid Value) : IGuidId, IParsable<NoteId>
{
    public static NoteId New() => new(Guid.CreateVersion7());

    public static NoteId Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var id) ? id : throw new FormatException($"'{s}' is not a note id.");

    public static bool TryParse(string? s, IFormatProvider? provider, out NoteId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

internal sealed class Note
{
    public NoteId Id { get; set; } = NoteId.New();
    public required string Title { get; set; }
    public string? Body { get; set; }
    public Guid OwnerUserId { get; set; }            // AppUser's key is Guid (AUTH-02)
    public AppUser Owner { get; set; } = null!;     // navigation (DATA-15)
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new NoteId(value))
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(4000);
        builder.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        builder.HasOne(x => x.Owner).WithMany(u => u.Notes).HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

`ToString()` is the Guid string, so `CreatedAtRoute(..., new { id = note.Id })` writes `/api/notes/{guid}`. JSON uses that same string, not `{ "value": "..." }`. A foreign key that points at `Note` is a `NoteId` and uses the same `HasConversion`. `OwnerUserId` stays `Guid` because it points at `AppUser`.

## Identity

Identity keys stay `Guid`, not a typed id (AUTH-02). `CurrentUser.UserId` is that `Guid`.

```csharp
// Data/AppUser.cs, Data/AppRole.cs, Data/AppDbContext.cs (namespace {App}.Api.Data)
public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();
    public string? DisplayName { get; set; }
    internal ICollection<Note> Notes { get; } = [];
}

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();
    public AppRole(string name) : base(name) => Id = Guid.CreateVersion7();
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

## Infrastructure

```csharp
public static class InfrastructureSetup
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddHealthChecks();
        services.AddProblemDetails(); // with UseExceptionHandler: unexpected exceptions -> 500 ProblemDetails (ERR-01)
        services.AddValidation();
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddOpenApi(o => o.AddSchemaTransformer((schema, context, _) =>
        {
            // JsonSchemaType is Microsoft.OpenApi. A JsonConverter otherwise becomes an empty schema, and IParsable becomes an object.
            var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (typeof(IGuidId).IsAssignableFrom(type))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = "uuid";
                schema.Properties?.Clear();
                schema.Required?.Clear();
            }
            return Task.CompletedTask;
        }));

        // Resolve the connection string lazily so tests can override configuration.
        // Design-time tools build the host too: run `dotnet ef` with ConnectionStrings__Default set.
        services.AddDbContext<AppDbContext>((sp, o) => o.UseNpgsql(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not set.")));

        services.AddAuthEndpoints<AppUser, AppRole, AppDbContext>(o =>
        {
            o.Passkeys.Enabled = false; // or ServerDomain + AddPasskeyUserIdFactory (AUTH-03)
        });
        services.AddTransient<IEmailSender<AppUser>, SmtpEmailSender>();
        services.ConfigureApplicationCookie(o =>
        {
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });

        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}

// MOD-10: one transaction per command. A nested call joins the caller's transaction.
public static class DbTransactions
{
    public static Task<Result> InTransactionAsync(
        this AppDbContext db, Func<CancellationToken, Task<Result>> work, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (db.Database.CurrentTransaction is not null) return await work(ct);

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            if (result.Problem is not null)
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }
            await tx.CommitAsync(ct);
            return result;
        });
}

public static class PermissionPolicies
{
    public static AuthorizationBuilder AddPermission(
        this AuthorizationBuilder auth, string permission, params string[] roles)
        => auth.AddPolicy(permission, p => p
            .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(roles));
}
```

## Shared kernel

```csharp
public static class Problems
{
    public static ProblemHttpResult BadRequest(string title, string detail)
        => TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
    public static ProblemHttpResult NotFound(string title, string detail)
        => TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);
    public static ProblemHttpResult Conflict(string title, string detail)
        => TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);
}

// ERR-04: what a service returns when it can fail. A value or a problem, nothing else.
public readonly record struct Result<T>
{
    private Result(T? value, ProblemHttpResult? problem) { Value = value; Problem = problem; }
    public T? Value { get; }
    public ProblemHttpResult? Problem { get; }
    public static implicit operator Result<T>(T value) => new(value, null);
    public static implicit operator Result<T>(ProblemHttpResult problem) => new(default, problem);
}

public readonly record struct Result
{
    private Result(ProblemHttpResult? problem) => Problem = problem;
    public ProblemHttpResult? Problem { get; }
    public static Result Success => default;
    public static implicit operator Result(ProblemHttpResult problem) => new(problem);
}

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public Guid? UserId => Guid.TryParse(
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid RequiredUserId => UserId ?? throw new InvalidOperationException("No authenticated user.");
}

public interface IDataSeeder
{
    int Order { get; }
    bool IsCritical { get; }
    Task SeedAsync(CancellationToken ct);
}
```

`Shared/GuidIds.cs`. Every typed id uses this converter (DATA-04). `[JsonConverter]` on the id type is what HTTP, OpenAPI, and `ReadFromJsonAsync` in tests all see.

```csharp
public interface IGuidId
{
    Guid Value { get; }
}

public sealed class GuidIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, IGuidId, IParsable<TId>
{
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        return text is not null && TId.TryParse(text, null, out var id)
            ? id
            : throw new JsonException($"Expected a Guid string for {typeof(TId).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
```

Endpoint side of ERR-04: `if (result.Problem is { } problem) return problem;` then build the success result from `result.Value`. `ProblemHttpResult` fits any `Results<..., ProblemHttpResult>` union, so no per-endpoint mapping is needed. C# does not allow implicit conversions from interfaces, so `T` must be a concrete type (a record).

`CurrentUser.RequiredUserId` and the connection-string check throw because those failures are bugs or broken configuration, not expected outcomes (ERR-05). They become a 500.

`DatabaseInitializer` is a plain `IHostedService` (not `BackgroundService`, so startup waits for it). In `StartAsync` it creates a scope, runs `MigrateAsync()` when `Database:MigrateOnStartup` is true (Development and tests only, DATA-10), then runs every `IDataSeeder` by `Order`. It rethrows when a critical seeder fails and logs otherwise.

## Feature registration

```csharp
public static class NotesModule
{
    public static IServiceCollection AddNotesModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<NoteService>();
        services.AddAuthorizationBuilder()
            .AddPermission(NotesPermissions.View, AppRoles.Member, AppRoles.Admin)
            .AddPermission(NotesPermissions.Manage, AppRoles.Member, AppRoles.Admin);
        return services;
    }

    public static IEndpointRouteBuilder MapNotesModule(this IEndpointRouteBuilder api)
    {
        api.MapNoteApi();
        return api;
    }
}

public static class NotesPermissions
{
    public const string View = "Notes.View";
    public const string Manage = "Notes.Manage";
}
```

## Endpoints and DTOs

```csharp
public static class NoteApi
{
    public static IEndpointConventionBuilder MapNoteApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/notes").WithTags("Notes");

        group.MapGet("/", ListNotes).WithName("ListNotes").WithSummary("List my notes")
            .RequireAuthorization(NotesPermissions.View);
        group.MapGet("/{id:guid}", GetNote).WithName("GetNote").WithSummary("Get a note")
            .RequireAuthorization(NotesPermissions.View);
        group.MapPost("/", CreateNote).WithName("CreateNote").WithSummary("Create a note")
            .RequireAuthorization(NotesPermissions.Manage).RequireAntiforgery();
        group.MapPost("/{id:guid}/archive", ArchiveNote).WithName("ArchiveNote").WithSummary("Archive a note")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(NotesPermissions.Manage).RequireAntiforgery();

        return group;
    }

    private static async Task<Ok<PaginatedItems<NoteListItem>>> ListNotes(
        AppDbContext db, CurrentUser user, [AsParameters] PaginationRequest page,
        [Description("Filter by title.")] string? search, CancellationToken ct)
    {
        var userId = user.RequiredUserId;
        var query = db.Notes.AsNoTracking().Where(n => n.OwnerUserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => EF.Functions.ILike(n.Title, $"%{search.Trim()}%"));

        var result = await Pagination.CreateAsync(page.PageIndex, page.PageSize,
            query.OrderByDescending(n => n.CreatedAt).Select(n => new NoteListItem(n.Id, n.Title, n.CreatedAt)), ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<NoteResponse>, NotFound>> GetNote(
        NoteId id, AppDbContext db, CurrentUser user, CancellationToken ct)
    {
        var note = await db.Notes.AsNoTracking()
            .Where(n => n.Id == id && n.OwnerUserId == user.RequiredUserId)
            .Select(n => new NoteResponse(n.Id, n.Title, n.Body, n.CreatedAt, n.ArchivedAt))
            .FirstOrDefaultAsync(ct);
        return note is null ? TypedResults.NotFound() : TypedResults.Ok(note);
    }

    private static async Task<Results<CreatedAtRoute<NoteResponse>, ProblemHttpResult>> CreateNote(
        CreateNoteRequest request, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.RequiredUserId;
        var title = request.Title.Trim();
        if (await db.Notes.AnyAsync(n => n.OwnerUserId == userId && n.Title == title, ct))
            return Problems.Conflict("Duplicate note", $"You already have a note titled '{title}'.");

        var note = new Note { Title = title, Body = request.Body, OwnerUserId = userId, CreatedAt = clock.GetUtcNow() };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);

        var body = new NoteResponse(note.Id, note.Title, note.Body, note.CreatedAt, null);
        return TypedResults.CreatedAtRoute(body, "GetNote", new { id = note.Id }); // 201 + Location: /api/notes/{id}
    }

    // Logic lives in NoteService because another feature also archives notes (MOD-05, ERR-04).
    private static async Task<Results<NoContent, ProblemHttpResult>> ArchiveNote(
        NoteId id, NoteService notes, CurrentUser user, CancellationToken ct)
    {
        var result = await notes.ArchiveAsync(id, user.RequiredUserId, ct);
        if (result.Problem is { } problem) return problem;
        return TypedResults.NoContent();
    }
}

public sealed record CreateNoteRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Title,
    [property: StringLength(4000)] string? Body);

public sealed record NoteResponse(NoteId Id, string Title, string? Body, DateTimeOffset CreatedAt, DateTimeOffset? ArchivedAt);

public sealed record NoteListItem(NoteId Id, string Title, DateTimeOffset CreatedAt);
```

`[property: ...]` on positional records is picked up by `AddValidation()` in .NET 10 (checked: an empty `Title` returns 400 `application/problem+json`). `RequireAntiforgery()` on a route handler comes from AuthEndpoints: add `using AuthEndpoints.Identity;`.

## Write service

`Notes/NoteService.cs`. Notes owns the state of `Note` (MOD-05). Another feature that archives notes calls this service inside its own transaction (MOD-10); the service saves but never opens a transaction.

```csharp
namespace {App}.Api.Notes;

public sealed class NoteService(AppDbContext db, TimeProvider clock)
{
    public async Task<Result> ArchiveAsync(NoteId noteId, Guid userId, CancellationToken ct)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.OwnerUserId == userId, ct);
        if (note is null) return Problems.NotFound("Note not found", $"Note {noteId} does not exist.");
        if (note.ArchivedAt is not null) return Problems.Conflict("Already archived", "This note is already archived.");

        note.ArchivedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
```

## Cross-feature read

`Reports/ActivityReportApi.cs` reads notes and users through navigations, with method syntax (MOD-04, MOD-09). It calls no feature service and nothing depends on it.

```csharp
private static async Task<Ok<List<ActivityRow>>> GetActivity(AppDbContext db, CancellationToken ct)
{
    var rows = await db.Users.AsNoTracking()
        .Select(u => new ActivityRow(
            u.Id,
            u.DisplayName,
            u.Notes.Count(n => n.ArchivedAt == null),
            u.Notes.Max(n => (DateTimeOffset?)n.CreatedAt)))
        .ToListAsync(ct);
    return TypedResults.Ok(rows);
}

public sealed record ActivityRow(Guid UserId, string? DisplayName, int OpenNotes, DateTimeOffset? LastNoteAt);
```

## Reusable code

Pure logic is static (CODE-01):

```csharp
internal static class NoteTitle
{
    public static string Normalize(string title) => title.Trim().ToUpperInvariant();
}

internal static class NoteMapping
{
    public static NoteResponse ToResponse(this Note n) => new(n.Id, n.Title, n.Body, n.CreatedAt, n.ArchivedAt);
}
```

Use `ToResponse()` on entities already in memory. In queries, keep the `Select(n => new NoteResponse(...))` projection (DTO-04); EF cannot translate a method call into SQL columns.

A service when it needs I/O, time, or the user (CODE-02, DI-01):

```csharp
internal sealed class NoteExportService(AppDbContext db, TimeProvider clock, ILogger<NoteExportService> log)
{
    public async Task<byte[]> ExportAsync(Guid userId, CancellationToken ct) { /* ... */ }
}
// NotesModule.AddNotesModule: services.AddScoped<NoteExportService>();
```

An interface only for an external dependency tests must fake (CODE-03):

```csharp
public interface IFileStorage { Task<Uri> PutAsync(string key, Stream body, CancellationToken ct); }
internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage { /* ... */ }
```

Typed HTTP client (DI-05):

```csharp
internal sealed class ExternalServiceClient(HttpClient http)
{
    public Task<ExternalItem?> GetAsync(string key, CancellationToken ct)
        => http.GetFromJsonAsync<ExternalItem>($"items/{key}", ct);
}
// services.AddHttpClient<ExternalServiceClient>(c => c.BaseAddress = new Uri(config["ExternalService:BaseUrl"]!));
```

Background work creates a scope per unit of work (DI-04):

```csharp
internal sealed class ArchiveOldNotesJob(IServiceScopeFactory scopes, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = clock.GetUtcNow().AddDays(-365);
            await db.Notes.Where(n => n.ArchivedAt == null && n.CreatedAt < cutoff)
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.ArchivedAt, clock.GetUtcNow()), ct);
        }
    }
}
```

This is the one place outside registration where `scope.ServiceProvider` is allowed. The CODE-05 archtest exempts types that implement `IHostedService`.

Traps and fixes:

| Trap | Fix |
|---|---|
| Singleton takes `AppDbContext` or another scoped service (captive dependency) | Make it scoped, or inject `IServiceScopeFactory` |
| `private static List<T> _cache = new()` | `IMemoryCache` or a singleton with `ConcurrentDictionary`; static fields hold immutable values only |
| `await Task.WhenAll(db.A.ToListAsync(), db.B.ToListAsync())` | Await in sequence, or one scope per branch |
| `new HttpClient()` | Typed client via `AddHttpClient<T>()` |
| `sp.GetRequiredService<X>()` in a handler or service | Constructor or handler parameter |
| `IOptionsSnapshot<T>` in a singleton | `IOptionsMonitor<T>` |
| `services.BuildServiceProvider()` in setup | `AddOptions<T>().Configure<TDep>(...)` or a factory overload |
| Transient `IDisposable` resolved from root | Scoped, or create and dispose it explicitly |

Host-build test (DI-06):

```csharp
[Collection("api")]
public sealed class ContainerTests(ApiFactory factory)
{
    [Fact]
    public void Service_graph_is_valid()
    {
        // Accessing Services builds the host; ValidateOnBuild and ValidateScopes throw on a bad graph.
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
```

`ValidateOnBuild` checks registered services only. It does not see minimal API handler parameters: an unregistered type in a handler is treated as a body or query parameter instead of failing at startup. Endpoint integration tests cover that.

## Test

Packages: `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.Extensions.TimeProvider.Testing`.

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync() => await _db.StartAsync();
    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); await _db.DisposeAsync(); }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:AdminPassword", "Test!Passw0rd");
    }

    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/identity/login", new { email, password })).EnsureSuccessStatusCode();
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/identity/csrfToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.CsrfToken);
        return client;
    }

    private sealed record CsrfTokenResponse(string CsrfToken); // a member cannot share its type's name (CS0542)
}

[CollectionDefinition("api")] public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

[Collection("api")]
public sealed class NoteApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Anonymous_list_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_with_location()
    {
        var client = await factory.SignInAsync("admin@example.test", "Test!Passw0rd");
        var response = await client.PostAsJsonAsync("/api/notes", new { title = $"Note {Guid.CreateVersion7()}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/notes/{created!.Id}", response.Headers.Location!.ToString());

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_validation_problem()
    {
        var client = await factory.SignInAsync("admin@example.test", "Test!Passw0rd");
        var response = await client.PostAsJsonAsync("/api/notes", new { title = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
```

OpenAPI exposure (DOC-02). `WithWebHostBuilder` keeps the parent's database settings:

```csharp
[Collection("api")]
public sealed class OpenApiExposureTests(ApiFactory factory)
{
    [Theory]
    [InlineData(null, HttpStatusCode.NotFound)]
    [InlineData("false", HttpStatusCode.NotFound)]
    [InlineData("true", HttpStatusCode.OK)]
    public async Task Production_serves_openapi_only_with_the_flag(string? flag, HttpStatusCode expected)
    {
        using var app = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            if (flag is not null) b.UseSetting("OpenApi:Enabled", flag);
        });
        var client = app.CreateClient();

        Assert.Equal(expected, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync("/scalar/v1")).StatusCode); // Scalar.AspNetCore 2.x default route
    }
}
```

Production mode in tests needs what Production needs: a registered `IEmailSender<AppUser>`, and passkeys either off or given a `ServerDomain`.

## Optional pattern: multi-tenancy

Not part of the default skill. Add only when the application needs tenants.

- A `Tenancy/` feature owns the state of `Data/Tenant.cs` and provides `Tenancy/CurrentTenant` (`TenantId` parsed from a `tenant_id` claim, the Guid string). Add the claim at sign-in with a custom `IUserClaimsPrincipalFactory<AppUser>` so requests do not hit the database.
- `TenantId` is that entity's typed id (DATA-04) and lives in `Shared/`, so `ITenantOwned` does not make `Shared/` depend on `Data/`.
- Tenant-owned entities implement `Shared/ITenantOwned { TenantId TenantId { get; set; } }`. The filter uses the scalar `TenantId`; a navigation to `Tenant` is optional.
- `AppDbContext` takes `CurrentTenant` in its constructor and, after `ApplyConfigurationsFromAssembly`, applies a named filter to every `ITenantOwned` type: `builder.Entity<T>().HasQueryFilter("Tenant", e => e.TenantId == TenantId)` where `TenantId` is a context property. This is the one allowed addition to `OnModelCreating` (DATA-01).
- A `SaveChangesInterceptor` sets `TenantId` on added `ITenantOwned` rows and rejects changes to it on modified rows.
- Cross-tenant reads use `IgnoreQueryFilters(["Tenant"])` only in endpoints guarded by a platform-admin permission. Flag every use in review, or ban the parameterless `IgnoreQueryFilters` in `BannedSymbols.txt`.
- Unique indexes on tenant-owned tables include `TenantId`.
- Tests: one fixture seeds two tenants and asserts each endpoint cannot read the other tenant's rows.
