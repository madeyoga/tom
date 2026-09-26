---
name: aspnetcore-webapi-structure
description: >-
  use this when scaffolding or reviewing ASP.NET Core 10 Web APIs built as a
  single-project modular monolith (minimal APIs, TypedResults, EF Core +
  PostgreSQL, cookie Identity via AuthEndpoints, Guid Identity keys, OpenAPI +
  Scalar). Covers layout, module boundaries, endpoints, DTOs, errors, auth,
  data, reusable code and DI lifetimes, seeding, and tests.
---
# ASP.NET Core 10 Web API structure

One web project, organized as a modular monolith. Every rule has an ID and an enforcement tag:

- `[banned]`: BannedApiAnalyzers (`BannedSymbols.txt`)
- `[analyzer]`: built-in or NuGet analyzer, set in `.editorconfig`
- `[archtest]`: architecture or reflection test in the test project
- `[review]`: human or agent review only

Exact checks are in [enforceable-checks.md](enforceable-checks.md). Module rules are in [module-boundaries.md](module-boundaries.md). A worked example module is in [reference.md](reference.md).

In an existing repo, match what is already there and list deviations. Do not refactor to this skill unless asked.

## Layout

```
src/{App}.Api/
├── {App}.Api.csproj          # Microsoft.NET.Sdk.Web, net10.0
├── Program.cs                # composition only
├── BannedSymbols.txt
├── Infrastructure/           # AppDbContext, auth, OpenAPI, errors, seeding host
├── Shared/                   # shared kernel: pagination, problems, Result, current user
├── Identity/                 # module: AppUser, AppRole, users API
├── {Module}/                 # one folder per feature or domain module
│   ├── {Module}Module.cs     # AddXModule + MapXModule
│   ├── {Module}Permissions.cs
│   ├── {Entity}.cs
│   ├── {Entity}Configuration.cs
│   ├── {Resource}Api.cs      # endpoints + their DTOs
│   ├── {Name}Service.cs      # optional, concrete
│   ├── Models/ Apis/ Services/  # optional split when the module grows (MOD-07)
│   └── Contracts/            # the only part other modules may use
└── Migrations/
tests/{App}.Api.Tests/        # integration + architecture tests
```

- **STR-01** One project, `Microsoft.NET.Sdk.Web`, `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`. No class-library layers (Domain/Application/Infrastructure projects). `[archtest]`
- **STR-02** Top-level folders are `Infrastructure/`, `Shared/`, `Migrations/`, and module folders. Nothing else. `[archtest]`
- **STR-03** Namespace = folder path: `{App}.Api.{Module}`, `{App}.Api.{Module}.Contracts`. `[analyzer]` (IDE0130)
- **STR-04** Never create `Controllers/`, `DTOs/`, `Interfaces/`, `Repositories/`, or `Helpers/` folders. `Models/`, `Apis/`, and `Services/` are allowed only as direct children of a module folder (MOD-07), never at the top level. `[archtest]`
- **STR-05** No MVC controllers. `[banned]` (`ControllerBase`)
- **STR-06** No Swashbuckle, MediatR, AutoMapper, or FluentValidation package references by default. `[archtest]` (csproj scan)

## Modules

- **MOD-01** Each module has one static `{Module}Module` class with `Add{Module}Module(this IServiceCollection, IConfiguration)` and `Map{Module}Module(this IEndpointRouteBuilder)`. `[archtest]`
- **MOD-02** `AddXModule` registers the module's services, seeders, and permission policies. `MapXModule` calls the module's `MapXApi` methods. `[review]`
- **MOD-03** Code in module A may use only `{App}.Api.{B}.Contracts` from module B. Never B's entities, configurations, services, or endpoint DTOs. `[archtest]`
- **MOD-04** Reference another module's rows by ID only. No navigation properties across modules. `[archtest]` (EF model test)
- **MOD-05** A module writes only its own tables. To change another module's data, call its Contracts service. `[review]`
- **MOD-06** `Shared/` depends on no module. `Infrastructure/` may reference module types only to compose them (DbContext, auth). `[archtest]`
- **MOD-07** When a module grows past about 5 files of one kind (entities, `*Api.cs`, services) or one file passes about 300 lines, split that kind into `{Module}/Models/`, `{Module}/Apis/`, or `{Module}/Services/`. The split stays inside the module. Namespaces follow the folders (`{App}.Api.{Module}.Models`). `Contracts/` stays the only surface other modules may use. `[review]`

## Program.cs

- **PRG-01** `Program.cs` contains host setup, `AddInfrastructure`, one `AddXModule` per module, the middleware pipeline, one `MapXModule` per module, and nothing else. No endpoint handlers, no policies, no `AddScoped<T>` for module services. `[review]`
- **PRG-02** End the file with `public partial class Program;` so tests can use `WebApplicationFactory<Program>`. `[archtest]`

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; }); // DI-06

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment); // db, auth, OpenAPI, errors, TimeProvider
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddNotesModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthEndpoints();           // authentication, authorization, rate limiting, antiforgery

// DOC-02: Development, or OpenApi:Enabled=true on a test/staging server. Never on real production.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapAuthEndpoints<AppUser>();  // /identity (+ /account for passkeys)

var api = app.MapGroup("/api");
api.MapIdentityModule();
api.MapNotesModule();

app.MapHealthChecks("/health");
app.Run();

public partial class Program;
```

## Endpoints

- **API-01** Minimal APIs only. One resource = one `public static class {Resource}Api` with `Map{Resource}Api(this IEndpointRouteBuilder)` in `{Module}/{Resource}Api.cs` (or `{Module}/Apis/` after a MOD-07 split). `[archtest]`
- **API-02** Handlers are named `private static async` methods. Inline lambdas only for one-line routes. `[review]`
- **API-03** Return `TypedResults.*`. Declare the exact union: `Task<Results<Ok<T>, NotFound>>`. Never use `Results.*` or `IResult` as a return type. `[banned]` + `[archtest]`
- **API-04** Routes: camelCase plural nouns (`/notes`, `/noteTags`). Constrain IDs (`{id:long}` or `{id:guid}`). Map literal routes before `{id}`. State changes that are not CRUD are `POST /{id}/{verb}` (`/archive`, `/approve`). `[review]`
- **API-05** Every endpoint has `.WithName("VerbResource")` (unique, becomes the operationId), `.WithSummary(...)`, and a group-level `.WithTags(...)`. `[archtest]`
- **API-06** Every endpoint has `.RequireAuthorization({Module}Permissions.X)` or an explicit `.AllowAnonymous()`. `[archtest]`
- **API-07** With cookie auth, every POST/PUT/PATCH/DELETE under `/api` has `.RequireAntiforgery()`. `[archtest]`
- **API-08** Every async handler takes a `CancellationToken` and passes it to every async call. `[analyzer]` (CA2016)
- **API-09** Status codes follow this table. `[review]`, plus `[archtest]` for creates (POST to a collection route returns `Created`/`CreatedAtRoute`)

| Operation | Result |
|---|---|
| List | `Ok<PaginatedItems<TResponse>>` with `[AsParameters] PaginationRequest` (`PageIndex` starts at 1, default size 10) |
| Get by ID | `Results<Ok<TResponse>, NotFound>` |
| Create | `Results<CreatedAtRoute<TResponse>, ProblemHttpResult>`: 201 with the body and a `Location` header, via `TypedResults.CreatedAtRoute(dto, "GetResource", new { id })` |
| Update, action with no body | `Results<NoContent, NotFound, ProblemHttpResult>` |
| Delete | `Results<NoContent, NotFound>` |
| File | `FileContentHttpResult` / `FileStreamHttpResult` |

- **API-10** Simple CRUD stays in the handler with `AppDbContext`. Move logic to a concrete `{Name}Service` only when it spans several entities, is reused, or needs a transaction. `[review]`

## DTOs

- **DTO-01** HTTP DTOs live in the same file as their `{Resource}Api` class, below it. `[review]`
- **DTO-02** DTOs are `public sealed record`. Names: `{Verb}{Resource}Request`, `{Resource}Response`, `{Resource}ListItem`. No `Dto` suffix. `[archtest]`
- **DTO-03** Never bind or return an entity type, at any depth. `[archtest]` (endpoint metadata vs EF model)
- **DTO-04** Map in the query: `.Select(x => new NoteResponse(...))`. Do not load entities just to map them. `[review]`
- **DTO-05** HTTP DTOs are not reused across modules. Cross-module shapes go in `Contracts/`. `[archtest]`
- **DTO-06** Use `DateTimeOffset` for instants, `DateOnly` for calendar dates. Serialize enums as strings. `[review]`

## Errors and validation

- **ERR-01** Register `AddProblemDetails()`, `UseExceptionHandler()`, and `UseStatusCodePages()`. Unexpected exceptions become a 500 ProblemDetails with no exception details outside Development. This is the only exception handling. Do not add an `IExceptionHandler` that turns exceptions into 4xx. `[archtest]`
- **ERR-02** Validate input with `builder.Services.AddValidation()` and DataAnnotations on request records. Invalid input returns 400 `ValidationProblem` automatically. Cross-field checks in the handler return `TypedResults.ValidationProblem(errors)`. Do not hand-write required or length checks. `[review]`
- **ERR-03** Expected failures are return values, never exceptions. Endpoints return `TypedResults.Problem(...)` through the `Shared/Problems` helpers (`Problems.Conflict(title, detail)`, `Problems.BadRequest(...)`) or `TypedResults.NotFound()`, and declare them in the `Results<...>` union. `[review]`
- **ERR-04** A service or Contracts method that can fail returns `Shared/Result<T>` (or `Result` without a value): either a value or a `ProblemHttpResult` built with `Problems.*`. The endpoint maps it with `if (result.Problem is { } problem) return problem;`. Reason: the service stays independent of each endpoint's `Results<...>` union, and one line maps it. `[review]`
- **ERR-05** Do not define exception types. Do not throw to signal an expected outcome (not found, conflict, rule violation). Throw only for bugs and broken infrastructure. `[archtest]` (no `Exception` subclasses) + `[review]` (throw sites)
- **ERR-06** Never put exception messages, stack traces, or SQL in a response. `[review]`

## Auth and permissions

- **AUTH-01** Use the `AuthEndpoints` NuGet package (3.x) facade: `AddAuthEndpoints<AppUser, AppRole, AppDbContext>(o => ...)`, `UseAuthEndpoints()` after `UseExceptionHandler()`, `MapAuthEndpoints<AppUser>()`. Do not call `AddIdentityApiEndpoints` yourself. Check https://madeyoga.github.io/AuthEndpoints before using any other AuthEndpoints API. To drop public self-registration, compose `MapCookieAuthEndpoints<AppUser>()` with a management map that omits `/register` (see the "composables" docs) instead of the facade map. `[review]`
- **AUTH-02** Identity keys are `Guid`: `AppUser : IdentityUser<Guid>`, `AppRole : IdentityRole<Guid>`, `AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>`. `AppUser` sets `Id = Guid.CreateVersion7()` in its constructor. `[archtest]`
- **AUTH-03** If passkeys stay enabled, register `AddPasskeyUserIdFactory(() => Guid.CreateVersion7().ToString())` so passkey sign-up does not mint v4 IDs, and set `Passkeys.ServerDomain`. Otherwise set `o.Passkeys.Enabled = false`. `[review]`
- **AUTH-04** Cookie sign-in is the default. Enable JWT or bearer only for a named non-browser client. `[review]`
- **AUTH-05** Register a real `IEmailSender<AppUser>` in Production. Keep `RequireConfirmedAccount = true` unless accounts are created by staff. `[review]`
- **AUTH-06** API requests without a session get 401, not a 302 redirect. Forbidden gets 403. `[archtest]` (integration test)
- **AUTH-07** Permissions are string constants `"{Area}.{Action}"` in `{Module}/{Module}Permissions.cs`. One policy per constant, registered in `AddXModule`. Endpoints reference permissions, never role names. `[archtest]`
- **AUTH-08** Read the current user through `Shared/CurrentUser` (claims only, no DB call). Never trust a user ID from the request body. `[review]`

## Data

- **DATA-01** One `AppDbContext` in `Infrastructure/Data/`. `OnModelCreating` calls `base.OnModelCreating` and `ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`, nothing else. `[review]`
- **DATA-02** One `IEntityTypeConfiguration<T>` per entity, next to the entity, in its module. No data annotations for mapping. `[archtest]` + `[banned]` (mapping attributes)
- **DATA-03** `AppDbContext` has no `DbSet` properties for module entities. Each module exposes its sets through an internal extension (`db.Notes()` returns `db.Set<Note>()`). `[archtest]`
- **DATA-04** Entity keys: default `long` identity columns. Use `Guid` v7 when the key is exposed publicly and enumeration matters, or the client creates the key (offline or idempotent create). Record the project default in the README. All entities in one aggregate use the same key type. `[review]`
- **DATA-05** Guid keys are set in the app with `Guid.CreateVersion7()` and configured `ValueGeneratedNever()`. Never `Guid.NewGuid()` (v4, poor index locality). `[banned]` + `[archtest]` (EF model)
- **DATA-06** EF calls are async: `SaveChangesAsync`, `ToListAsync`, `FirstOrDefaultAsync`, `MigrateAsync`. `[banned]` for `SaveChanges`/`Migrate`, `[review]` for sync LINQ.
- **DATA-07** Reads use `AsNoTracking()` and project to DTOs. `[review]`
- **DATA-08** Get time from the injected `TimeProvider` (registered as `TimeProvider.System`). Never `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, or `DateTimeOffset.UtcNow`. Store instants as `timestamptz` in UTC. `[banned]`
- **DATA-09** Raw SQL uses `FromSql`/`SqlQuery`/`ExecuteSql` with interpolation. Never the `*Raw` variants with concatenated strings. `[banned]`
- **DATA-10** Migrations: `dotnet ef migrations add {Name}`. Never edit a migration that has been applied anywhere. Apply migrations automatically only in Development; production runs a migration step or bundle. `[review]`
- **DATA-11** Strings get `HasMaxLength`, money gets `HasPrecision(18, 2)`, enums get `HasConversion<string>()`. `[review]`
- **DATA-12** Rows with concurrent writers get an `xmin` concurrency token and handle `DbUpdateConcurrencyException`. `[review]`
- **DATA-13** No repository or unit-of-work over EF. Handlers and services use `AppDbContext` directly. Interfaces follow CODE-03. `[archtest]`

## Reusable code and DI

Decide in this order. Examples are in reference.md, "Reusable code".

Static function or service:

- **CODE-01** Pure logic (no I/O, clock, config, state, or logging) is a `static` method or extension method: mapping, formatting, calculations, normalization. Put it in the module that owns the concept, or in `Shared/` if two modules need it. `[analyzer]` (CA1822)
- **CODE-02** Logic that needs I/O, `AppDbContext`, `HttpClient`, options, `TimeProvider`, the current user, or `ILogger`, or that a test must replace, is a DI service. `[review]`
- **CODE-03** Services are concrete classes. Add an interface only for two or more real implementations, or for an external dependency (email, storage, payment API) that tests must fake. `[archtest]`
- **CODE-04** Static fields are `const` or `static readonly` holding an immutable value (`FrozenDictionary`, `ImmutableArray`, string, record). No mutable static state. `[archtest]` + `[review]` for immutability of the value
- **CODE-05** Business code never resolves from `IServiceProvider` (no service locator). Take dependencies in the constructor or handler parameters. Only `Infrastructure/` hosting code and `*Module` registration may touch `IServiceProvider`. `[archtest]`

Lifetimes:

- **DI-01** Scoped is the default for application services, and required for anything that uses `AppDbContext`, `CurrentUser`, or other per-request data. `[review]`
- **DI-02** Singleton only for stateless types or thread-safe shared state: caches, options wrappers, clients. A singleton never depends on a scoped service, `IOptionsSnapshot<T>`, or `AppDbContext`. Use `IOptionsMonitor<T>` or `IOptions<T>` instead. `[archtest]` (ValidateScopes via the DI-06 host test)
- **DI-03** Transient only for lightweight stateless types, and rarely. No transient `IDisposable`: the container keeps it until the scope ends, and one resolved from the root provider is never released. `[archtest]` (service descriptors)
- **DI-04** Singletons and hosted services that need scoped work inject `IServiceScopeFactory` and create one scope per unit of work (per message, per timer tick, per seeding run). Hosted services never take `AppDbContext` in the constructor. `[archtest]`
- **DI-05** Outgoing HTTP uses `IHttpClientFactory`: `services.AddHttpClient<TClient>()` typed clients. Never `new HttpClient()`. `[banned]`
- **DI-06** Turn on `ValidateScopes` and `ValidateOnBuild` in every environment (`builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; })`). They are on only in Development by default. A test starts the host so a bad graph fails CI. `[archtest]`
- **DI-07** Never call `BuildServiceProvider()` while registering services. Use `AddOptions<T>().Configure<TDep>(...)`, `IConfigureOptions<T>`, or a factory overload. `[banned]` + `[analyzer]` (ASP0000)
- **DI-08** `AppDbContext` is not thread-safe. No `Task.WhenAll`, `Parallel.ForEachAsync`, or unawaited queries over one context. Run queries in sequence, or give each parallel branch its own scope. `[review]`

## Seeding

- **SEED-01** Seed with `IDataSeeder` (`int Order`, `bool IsCritical`, `Task SeedAsync(CancellationToken)`) from `Shared/`. Modules register seeders in `AddXModule` with `AddScoped<IDataSeeder, XSeeder>()`. `[archtest]`
- **SEED-02** One `DatabaseInitializer` hosted service in `Infrastructure/Seeding/` runs seeders by `Order` in a scope. A critical seeder failure stops startup. A non-critical failure is logged. `[review]`
- **SEED-03** Seeders are idempotent: check before insert. Roles and the first admin come from seeders, never from handlers. Secrets come from configuration. `[review]`

## OpenAPI

- **DOC-01** `AddOpenApi()` + `Scalar.AspNetCore` (`MapScalarApiReference`). Never Swashbuckle. `[archtest]`
- **DOC-02** Map OpenAPI and Scalar only in Development or when `OpenApi:Enabled` (bool, default `false`) is `true`. The flag is for a test or staging server that runs with `ASPNETCORE_ENVIRONMENT=Production`. It stays off on real production: never set it in `appsettings.json` or `appsettings.Production.json`, only in that server's environment (`OpenApi__Enabled=true`). `[archtest]`
- **DOC-03** Document query parameters with `[Description]`. Mark deprecated operations in an `AddOperationTransformer`, not in comments. Do not use `.WithOpenApi()` (obsolete in .NET 10). `[review]`

## Testing

- **TEST-01** Integration tests use `WebApplicationFactory<Program>` against real PostgreSQL from `Testcontainers.PostgreSql`, one container per test collection, schema created with `MigrateAsync()`. No EF InMemory or SQLite. `[archtest]` (package scan)
- **TEST-02** Each endpoint has at least: success, 401 or 403, and 404 or 400. `[review]`
- **TEST-03** Sign in through the real `/identity/login` and send the antiforgery header from `/identity/csrfToken`. `[review]`
- **TEST-04** Replace `TimeProvider` with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) in tests that depend on time. `[review]`
- **TEST-05** The architecture tests from enforceable-checks.md live in the same test project and run in CI. `[archtest]`

## Done when

An agent may call a change done only when all of these hold:

- [ ] `dotnet build` passes with zero warnings (`TreatWarningsAsErrors`), including RS0030.
- [ ] `dotnet test` passes, including architecture tests.
- [ ] New module: `{Module}Module.cs` exists and `Program.cs` calls its `AddXModule` and `MapXModule`.
- [ ] New entity: configuration file, `db.X()` accessor, migration added and reviewed.
- [ ] New endpoint: name, summary, tag, permission or `AllowAnonymous`, antiforgery on unsafe verbs, `CancellationToken`, typed result union, 201 `CreatedAtRoute` for creates, expected failures as `TypedResults.Problem`.
- [ ] New permission: constant in `{Module}Permissions` and policy registered in `AddXModule`.
- [ ] No entity in any request or response. No cross-module use outside `Contracts`.
- [ ] New service: lifetime chosen by DI-01 to DI-03, concrete unless CODE-03 applies, no `IServiceProvider` in business code.
- [ ] Tests cover success, auth failure, and not-found or validation for each new endpoint.
- [ ] Deviations from this skill are listed in the PR description with a reason.
