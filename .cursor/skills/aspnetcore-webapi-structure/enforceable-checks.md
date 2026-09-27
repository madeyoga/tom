# Enforceable checks

How each SKILL.md rule can be checked by a machine, and which rules stay with review. Package versions are the latest stable on nuget.org on 2026-09-26. Pin them when adopting.

Compiled and run on 2026-09-26 in a scratch solution (SDK 10.0.401, EF Core 10.0.4 via Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Scalar.AspNetCore 2.17.10, AuthEndpoints 3.1.0, NetArchTest.Rules 1.3.2, Testcontainers.PostgreSql 4.15.0): the build passes with warnings as errors, all tests pass, and each ban, analyzer rule, and architecture rule was shown to fail on a deliberate violation. Re-check after major package upgrades.

## 1. Build settings

`Directory.Build.props` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <!-- IDE0005 (unused usings) only reports in build when docs are generated -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

API project only (`{App}.Api.csproj`):

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="5.6.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <AdditionalFiles Include="BannedSymbols.txt" />
</ItemGroup>
```

Pin `Microsoft.EntityFrameworkCore.Design` (and any direct EF Core package) to the EF Core version that `Npgsql.EntityFrameworkCore.PostgreSQL` depends on. A newer Design package makes the test project warn MSB3277 (EF Core version conflict).

Keep `BannedSymbols.txt` out of the test project. Tests may use `Guid.NewGuid()` for database names and `new HttpClient(handler)`.

## 2. BannedSymbols.txt (RS0030)

Format: `{kind}:{documentation ID};{message}`. `T:` type, `M:` method, `P:` property, `F:` field. Methods without parameters have no parentheses. Constructors are `#ctor`. Each overload needs its own line. A wrong ID is silently ignored, and a call that binds to an unlisted overload (`ExecuteSqlRawAsync(sql, ct)`, `Migrate(target)`) is not flagged. The list below was checked against EF Core 10.0.4 by calling every overload once.

```text
# DATA-05 keys
M:System.Guid.NewGuid;Use Guid.CreateVersion7() (DATA-05). Suppress with a reason only for non-key random values.

# DATA-08 / TEST-04 time
P:System.DateTime.Now;Inject TimeProvider and call GetUtcNow() (DATA-08)
P:System.DateTime.UtcNow;Inject TimeProvider and call GetUtcNow() (DATA-08)
P:System.DateTime.Today;Inject TimeProvider (DATA-08)
P:System.DateTimeOffset.Now;Inject TimeProvider and call GetUtcNow() (DATA-08)
P:System.DateTimeOffset.UtcNow;Inject TimeProvider and call GetUtcNow() (DATA-08)

# API-03 results
T:Microsoft.AspNetCore.Http.Results;Use TypedResults and a Results<...> union (API-03)

# STR-05 controllers
T:Microsoft.AspNetCore.Mvc.ControllerBase;Minimal APIs only (STR-05)
T:Microsoft.AspNetCore.Mvc.Controller;Minimal APIs only (STR-05)

# DATA-06 sync EF
M:Microsoft.EntityFrameworkCore.DbContext.SaveChanges;Use SaveChangesAsync(ct) (DATA-06)
M:Microsoft.EntityFrameworkCore.DbContext.SaveChanges(System.Boolean);Use SaveChangesAsync(ct) (DATA-06)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade);Use MigrateAsync(ct) (DATA-06)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String);Use MigrateAsync(ct) (DATA-06)
M:Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated;Use migrations (DATA-10)
M:Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreatedAsync(System.Threading.CancellationToken);Use migrations (DATA-10)

# DATA-09 raw SQL
M:Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlRaw``1(Microsoft.EntityFrameworkCore.DbSet{``0},System.String,System.Object[]);Use FromSql($"...") (DATA-09)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Object[]);Use ExecuteSql($"...") (DATA-09)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Collections.Generic.IEnumerable{System.Object});Use ExecuteSql($"...") (DATA-09)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Threading.CancellationToken);Use ExecuteSqlAsync($"...") (DATA-09)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Object[]);Use ExecuteSqlAsync($"...") (DATA-09)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Collections.Generic.IEnumerable{System.Object},System.Threading.CancellationToken);Use ExecuteSqlAsync($"...") (DATA-09)
M:Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQueryRaw``1(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Object[]);Use SqlQuery($"...") (DATA-09)

# DATA-02 mapping attributes
T:System.ComponentModel.DataAnnotations.Schema.TableAttribute;Map in IEntityTypeConfiguration (DATA-02)
T:System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;Map in IEntityTypeConfiguration (DATA-02)
T:System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute;Map in IEntityTypeConfiguration (DATA-02)
T:System.ComponentModel.DataAnnotations.Schema.InversePropertyAttribute;Map in IEntityTypeConfiguration (DATA-02)
T:System.ComponentModel.DataAnnotations.KeyAttribute;Map in IEntityTypeConfiguration (DATA-02)
T:Microsoft.EntityFrameworkCore.IndexAttribute;Map in IEntityTypeConfiguration (DATA-02)
T:Microsoft.EntityFrameworkCore.PrecisionAttribute;Map in IEntityTypeConfiguration (DATA-02)

# DI-05 HttpClient
M:System.Net.Http.HttpClient.#ctor;Use IHttpClientFactory or a typed client (DI-05)
M:System.Net.Http.HttpClient.#ctor(System.Net.Http.HttpMessageHandler);Use IHttpClientFactory or a typed client (DI-05)
M:System.Net.Http.HttpClient.#ctor(System.Net.Http.HttpMessageHandler,System.Boolean);Use IHttpClientFactory or a typed client (DI-05)

# DI-07 BuildServiceProvider
M:Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(Microsoft.Extensions.DependencyInjection.IServiceCollection);Use options/factory overloads (DI-07)
M:Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Boolean);Use options/factory overloads (DI-07)
M:Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(Microsoft.Extensions.DependencyInjection.IServiceCollection,Microsoft.Extensions.DependencyInjection.ServiceProviderOptions);Use options/factory overloads (DI-07)

# Optional: sync over async
M:System.Threading.Tasks.Task.Wait;Await instead
P:System.Threading.Tasks.Task`1.Result;Await instead

# Optional, tenancy pattern only
M:Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters``1(System.Linq.IQueryable{``0});Use IgnoreQueryFilters(["Tenant"]) in platform-admin endpoints only
```

Exception types are not banned here. "No custom exceptions" (ERR-05) is an architecture test in 4.2, because a type ban cannot express "do not derive from `Exception`".

Not bannable: `Guid.NewGuid()` only for keys (the ban is project-wide; suppress per line with `#pragma warning disable RS0030 // reason`), sync LINQ over `IQueryable` (`ToList()` on `IQueryable` and `IEnumerable` is one method), and `IOptionsSnapshot` in singletons (DI-06 catches it at startup).

## 3. .editorconfig

```ini
root = true

[*.cs]
# STR-03/STR-07 namespace = top-level folder, checked by the 4.8 source scan.
# IDE0130 wants a segment per subfolder (Apis/, Services/, Data/ subfolders), so it stays off.
dotnet_diagnostic.IDE0130.severity = none
dotnet_style_namespace_match_folder = false
csharp_style_namespace_declarations = file_scoped:warning

# API-08 forward CancellationToken
dotnet_diagnostic.CA2016.severity = warning
# CODE-01 mark pure members static
dotnet_diagnostic.CA1822.severity = warning
# seal internal types (entities, services, configurations)
dotnet_diagnostic.CA1852.severity = warning
# DI-07 BuildServiceProvider in ConfigureServices (built-in ASP.NET analyzer)
dotnet_diagnostic.ASP0000.severity = error
# banned APIs
dotnet_diagnostic.RS0030.severity = error
# EF internal API use
dotnet_diagnostic.EF1001.severity = error
# logging: structured templates
dotnet_diagnostic.CA2254.severity = warning
dotnet_diagnostic.CA1848.severity = suggestion
dotnet_diagnostic.CA1873.severity = suggestion
# unused usings
dotnet_diagnostic.IDE0005.severity = warning

# Shared/ is a folder name, not an API consumed from VB (latest-recommended flags the namespace)
dotnet_diagnostic.CA1716.severity = none

# ASP.NET apps have no SynchronizationContext
dotnet_diagnostic.CA2007.severity = none

# Leading **/ so it matches src/{App}.Api/Migrations from a repo-root .editorconfig
[**/Migrations/**.cs]
generated_code = true
dotnet_diagnostic.CA1822.severity = none
dotnet_diagnostic.IDE0005.severity = none

# Test naming: Method_names_with_underscores and xunit *Collection fixtures
[tests/**.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1711.severity = none
```

`AnalysisLevel` `latest-recommended` also turns on CA1873 (expensive logging arguments) and CA1716 (the `Shared` namespace is a VB keyword), which is why they are set above. Without the `**/` prefix the Migrations section does not match and EF-generated migrations fail the build (IDE0005, IDE0161, CA1861).

Built-in route analyzers (ASP0018 unused route parameter, ASP0022/ASP0023 ambiguous routes) are on by default. With `TreatWarningsAsErrors` they fail the build.

Optional third-party: `Meziantou.Analyzer` adds async and string-comparison checks. Add it only if the noise is acceptable, and turn off its ConfigureAwait rule (MA0004) for ASP.NET Core. Nothing in the skill depends on it.

## 4. Architecture tests

Put them in `tests/{App}.Api.Tests/Architecture/`. The test project uses `Microsoft.NET.Sdk`, so `IHostedService` needs `using Microsoft.Extensions.Hosting;`. Packages: `NetArchTest.Rules` 1.3.2 for type dependencies (Mono.Cecil, reads method bodies). `Microsoft.CodeAnalysis.CSharp` 5.9.0 for the folder-based source scans (4.8). Everything else is plain reflection plus the running app from `ApiFactory`. `TngTech.ArchUnitNET` + `TngTech.ArchUnitNET.xUnit` 0.13.4 is the alternative if you prefer its fluent rules; do not use both.

Shared helper:

```csharp
internal static class Arch
{
    public static readonly Assembly Api = typeof(Program).Assembly;
    public const string Root = "{App}.Api";
    public static readonly string[] Layers = ["Infrastructure", "Shared", "Data"];

    public static IReadOnlyList<Type> Types { get; } = Api.GetTypes()
        .Where(t => t.Namespace?.StartsWith(Root, StringComparison.Ordinal) == true)
        .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), false))
        .ToArray();

    public static string? FeatureOf(Type t)
    {
        var parts = t.Namespace?.Split('.') ?? [];
        var depth = Root.Split('.').Length;
        return parts.Length > depth && !Layers.Contains(parts[depth]) ? parts[depth] : null;
    }

    public static IReadOnlyList<string> Features { get; } =
        Types.Select(FeatureOf).OfType<string>().Distinct().Order().ToArray();
}
```

### 4.1 Type dependency tests (NetArchTest)

| Rule | Test |
|---|---|
| MOD-03, MOD-06, MOD-13 | Types in `{Root}.Data` (including `Data.Migrations`) do not depend on any feature namespace or on `{Root}.Infrastructure`. Types in `{Root}.Infrastructure` do not depend on any feature namespace. Types in `{Root}.Shared` depend on no feature, `Data`, or `Infrastructure` namespace. |
| CODE-05 | Types in feature namespaces, except `IHostedService` implementations (including `BackgroundService` subclasses) and `*Module` classes, do not depend on `System.IServiceProvider`, the `GetService`/`GetRequiredService` extension classes, or `IServiceScopeFactory`/`IServiceScope`. |
| STR-05 | No type inherits `Microsoft.AspNetCore.Mvc.ControllerBase` (backs up the ban). |

```csharp
[Fact]
public void Lower_layers_do_not_depend_on_features()
{
    string[] features = [.. Arch.Features.Select(f => $"{Arch.Root}.{f}")];
    (string Layer, string[] Forbidden)[] rules =
    [
        ("Data", [.. features, $"{Arch.Root}.Infrastructure"]),
        ("Infrastructure", features),
        ("Shared", [.. features, $"{Arch.Root}.Data", $"{Arch.Root}.Infrastructure"]),
    ];

    var failures = rules
        .SelectMany(r => Types.InAssembly(Arch.Api)
            .That().ResideInNamespaceMatching($@"^{Regex.Escape($"{Arch.Root}.{r.Layer}")}(\..+)?$")
            .ShouldNot().HaveDependencyOnAny(r.Forbidden)
            .GetResult().FailingTypeNames ?? [])
        .ToArray();

    Assert.True(failures.Length == 0, string.Join(", ", failures));
}

// NetArchTest does not see IServiceProvider when it only appears as a property return type
// (scope.ServiceProvider) or as the parameter of an extension call (GetRequiredService<T>),
// so the locator entry points are listed too.
private static readonly string[] Locator =
[
    "System.IServiceProvider",
    "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions",
    "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions",
    "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory",
    "Microsoft.Extensions.DependencyInjection.IServiceScope",
    "Microsoft.Extensions.DependencyInjection.AsyncServiceScope",
];

[Fact]
public void Business_code_does_not_use_the_service_locator()
{
    // NetArchTest's ImplementInterface sees direct interfaces only (a BackgroundService subclass
    // does not match), so the exemptions are computed with reflection.
    var exempt = Arch.Types
        .Where(t => typeof(IHostedService).IsAssignableFrom(t) || t.Name.EndsWith("Module", StringComparison.Ordinal))
        .Select(t => t.FullName!)
        .ToHashSet();

    var result = Types.InAssembly(Arch.Api)
        .That().ResideInNamespaceMatching($@"^{Regex.Escape(Arch.Root)}\.(?!(Infrastructure|Shared|Data)(\.|$))[^.]+(\..+)?$")
        .ShouldNot().HaveDependencyOnAny(Locator)
        .GetResult();

    var failures = (result.FailingTypeNames ?? []).Where(n => !exempt.Contains(n)).ToArray();
    Assert.True(failures.Length == 0, string.Join(", ", failures));
}
```

Matching by namespace is safe because every file in a top-level folder uses that folder's single namespace (STR-07, checked in 4.8). The regex anchors on a dot, and NetArchTest matches dependency names by dotted segments, so feature `Notes` does not also match `NotesArchive`. Dependencies between features are a source scan (4.8), because it also sees `using` directives and names in files that compile to no IL dependency.

Verified with NetArchTest.Rules 1.3.2: `ResideInNamespaceMatching` and `FailingTypeNames` exist; dependencies inside async methods and lambdas count for the declaring type; a feature named `NotesArchive` is not matched as `Notes`. The original CODE-05 version (`DoNotImplementInterface(typeof(IHostedService))` + `HaveDependencyOn("System.IServiceProvider")`) missed `scope.ServiceProvider.GetRequiredService<T>()` in async code and did not exempt `BackgroundService` subclasses.

### 4.2 Reflection tests on types

| Rule | Test |
|---|---|
| CODE-04 | No static field in `Arch.Types` that is neither `IsLiteral` nor `IsInitOnly`. No static property with a setter. No `static readonly` field whose type is an array, `List<>`, `Dictionary<,>`, `HashSet<>`, or other mutable collection. |
| DI-04 | No `IHostedService` implementation has a constructor parameter assignable to `DbContext`. |
| CODE-03, DATA-13 | Every interface declared in the API project has at least two implementations in the assembly, or is listed in `AllowedSingleImplementationInterfaces` in the test with a reason. No type named `*Repository` or `*UnitOfWork`. |
| API-01 | Every public static method named `Map*Api` is on a `public static class` named `*Api` in a feature namespace. |
| MOD-01 | Every feature has a static class `{Feature}Module` in namespace `{Root}.{Feature}` exactly with `Add{Feature}Module(IServiceCollection, IConfiguration)` and `Map{Feature}Module(IEndpointRouteBuilder)`. `Program.cs` text contains both calls. |
| AUTH-02 | `typeof(AppUser).BaseType == typeof(IdentityUser<Guid>)`, same for `AppRole`, `AppDbContext` derives from `IdentityDbContext<AppUser, AppRole, Guid>`, and `new AppUser().Id.Version == 7`. |
| AUTH-07 | Every `{Feature}Permissions` const matches `^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+$` and is unique. |
| DATA-03 | `typeof(AppDbContext).GetProperties(BindingFlags.DeclaredOnly | Public | Instance)` has no `DbSet<>` property. |
| SEED-01 | Every `IDataSeeder` implementation is registered (see 4.4) and has a distinct `Order`. |
| ERR-05 | No type in `Arch.Api` derives from `System.Exception`. Expected failures are `Result`/`ProblemHttpResult` values. |
| ERR-01 | No type in `Arch.Api` implements `IExceptionHandler` (unexpected exceptions go to the default `UseExceptionHandler` + ProblemDetails). An added handler needs an allowlist entry with a reason. |

```csharp
[Fact]
public void No_mutable_static_state()
{
    Type[] mutable = [typeof(List<>), typeof(Dictionary<,>), typeof(HashSet<>), typeof(Queue<>), typeof(Stack<>)];
    var bad = Arch.Types
        .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
        .Where(f => !f.IsLiteral && (!f.IsInitOnly
            || f.FieldType.IsArray
            || (f.FieldType.IsGenericType && mutable.Contains(f.FieldType.GetGenericTypeDefinition()))))
        .Select(f => $"{f.DeclaringType!.FullName}.{f.Name}");
    var props = Arch.Types
        .SelectMany(t => t.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .Where(p => p.SetMethod is not null)
        .Select(p => $"{p.DeclaringType!.FullName}.{p.Name}");
    Assert.Empty(bad.Concat(props));
}

[Fact]
public void Hosted_services_do_not_take_a_DbContext()
{
    var bad = Arch.Types
        .Where(t => typeof(IHostedService).IsAssignableFrom(t) && !t.IsAbstract)
        .Where(t => t.GetConstructors().SelectMany(c => c.GetParameters())
            .Any(p => typeof(DbContext).IsAssignableFrom(p.ParameterType)))
        .Select(t => t.FullName);
    Assert.Empty(bad);
}
```

### 4.3 Endpoint metadata tests (running app)

Read endpoints from the built app: `factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()`, filtered to routes starting with `/api`. Minimal API endpoints carry the handler `MethodInfo` in their metadata.

| Rule | Check on each `/api` endpoint |
|---|---|
| API-05 | Has `IEndpointNameMetadata` (names unique), `IEndpointSummaryMetadata`, and `ITagsMetadata`. |
| API-06 | Has `IAuthorizeData` with a non-empty `Policy`, or `IAllowAnonymous`. No `IAuthorizeData.Roles`. |
| AUTH-07 | Every policy name resolves through `IAuthorizationPolicyProvider.GetPolicyAsync` and is a `*Permissions` constant. |
| API-07 | POST/PUT/PATCH/DELETE (from `IHttpMethodMetadata`) have `IAntiforgeryMetadata { RequiresValidation: true }`. |
| API-03 | Handler return type is not `IResult`, `Task<IResult>`, or `ValueTask<IResult>`. |
| API-01, API-02 | Handler `MethodInfo.DeclaringType` is a static class named `*Api`. Handler is not compiler-generated (no lambdas), with an allowlist for exceptions. |
| DTO-03 | Walk the handler return type and every non-service parameter type: unwrap `Task<>`, `ValueTask<>`, `Results<...>`, `Ok<>` and other generic arguments, then public properties recursively. No type in the EF model (`db.Model.GetEntityTypes().Select(e => e.ClrType)`) may appear. Use `IServiceProviderIsService` to skip injected parameters. |
| DTO-02 | Every DTO found in that walk that belongs to a feature is `sealed`, is a record (has `<Clone>$`), and its name does not end with `Dto`. |
| API-09 | Every POST whose route pattern ends at the collection (no trailing route parameter or verb segment after one) returns a union containing `Created<T>` or `CreatedAtRoute<T>`, not `Ok<T>`. |
| ERR-03 | Handler return types that can fail include `ProblemHttpResult`, `ValidationProblem`, or `NotFound` in the union. Report-only: a union with only a success type is allowed. |
| DTO-05 | Every feature DTO in a handler signature is declared in the same feature namespace as the handler (`Apis/` subfolders share it, STR-07). `Shared` types (`PaginatedItems<T>`) are allowed. |

```csharp
private IEnumerable<RouteEndpoint> ApiEndpoints()
    => factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
        .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true);

// API-09: a POST to a collection route (no route parameter) returns Created<T> or CreatedAtRoute<T>.
[Fact]
public void Collection_posts_return_201()
{
    Type[] created = [typeof(Created<>), typeof(CreatedAtRoute<>), typeof(Created), typeof(CreatedAtRoute)];
    var failures = new List<string>();
    var checkedCount = 0;
    foreach (var endpoint in ApiEndpoints())
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
        if (!methods.Contains("POST") || endpoint.RoutePattern.Parameters.Count > 0) continue;
        checkedCount++;

        var returnType = endpoint.Metadata.GetMetadata<MethodInfo>()!.ReturnType;
        if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
            || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            returnType = returnType.GetGenericArguments()[0];
        var members = returnType.IsGenericType && typeof(INestedHttpResult).IsAssignableFrom(returnType)
            ? returnType.GetGenericArguments()
            : [returnType];
        if (!members.Any(t => created.Contains(t.IsGenericType ? t.GetGenericTypeDefinition() : t)))
            failures.Add($"POST {endpoint.RoutePattern.RawText} returns {returnType.Name}, expected Created/CreatedAtRoute");
    }
    Assert.True(checkedCount > 0, "No collection POST endpoints found under /api.");
    Assert.Empty(failures);
}

[Fact]
public void Endpoints_never_expose_entities()
{
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var isService = factory.Services.GetRequiredService<IServiceProviderIsService>();
    var entities = db.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

    var failures = new List<string>();
    foreach (var endpoint in ApiEndpoints())
    {
        var method = endpoint.Metadata.GetMetadata<MethodInfo>()!;
        var types = method.GetParameters()
            .Where(p => !isService.IsService(p.ParameterType) && p.ParameterType != typeof(CancellationToken))
            .Select(p => p.ParameterType)
            .Append(method.ReturnType);
        foreach (var hit in types.SelectMany(t => Reachable(t)).Where(entities.Contains).Distinct())
            failures.Add($"{endpoint.RoutePattern.RawText} exposes {hit.Name}");
    }
    Assert.Empty(failures);
}

private static IEnumerable<Type> Reachable(Type type, HashSet<Type>? seen = null)
{
    seen ??= [];
    if (!seen.Add(type) || type.IsPrimitive || type == typeof(string)
        || (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true && !type.IsGenericType))
        yield break;
    yield return type;
    IEnumerable<Type> next = type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments()
        .Concat(type.Namespace?.StartsWith(Arch.Root, StringComparison.Ordinal) == true
            ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.PropertyType)
            : []);
    foreach (var t in next)
    foreach (var r in Reachable(t, seen))
        yield return r;
}
```

### 4.4 Service registration tests

Capture descriptors with `factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => captured = s.ToList()))`.

| Rule | Check |
|---|---|
| DI-06 | Start the host (`factory.Services`) and resolve `AppDbContext` in a scope. With `ValidateOnBuild` and `ValidateScopes` set in `Program.cs`, a captive dependency or missing registration throws here. Also assert `Program.cs` contains `ValidateScopes = true` and `ValidateOnBuild = true`. |
| DI-02 | Covered by DI-06 at startup. |
| DI-03 | No descriptor with `Lifetime == Transient` whose `ImplementationType` is in `Arch.Api` and implements `IDisposable` or `IAsyncDisposable`. |
| SEED-01 | Every `IDataSeeder` implementation in `Arch.Api` has a `Scoped` descriptor. |

### 4.5 EF model tests

| Rule | Check |
|---|---|
| DATA-02 | Every entity CLR type in `{Root}.Data` has exactly one `IEntityTypeConfiguration<T>` implementation in `{Root}.Data`, except `AppUser` and `AppRole` when Identity's mapping is enough. No entity type lives outside `{Root}.Data`. The file placement is checked in 4.8. |
| DATA-05 | Every entity with a `Guid` primary key has `ValueGenerated == ValueGenerated.Never`. |
| DATA-04 | Report (not fail) the key types per entity so a mixed aggregate is visible in the test output. Mixing is a review call. |

### 4.6 Project and file scans

Find the repo root by walking up from `AppContext.BaseDirectory` to the `.sln`/`.slnx` file.

| Rule | Check |
|---|---|
| STR-01 | Exactly one non-test `.csproj` with `Sdk="Microsoft.NET.Sdk.Web"` and `net10.0`. |
| STR-02, STR-04 | Top-level folders of the API project are only `Infrastructure`, `Shared`, `Data`, `Properties`, `bin`, `obj`, and feature folders. `Migrations` appears only as `Data/Migrations`. No folder anywhere named `Controllers`, `DTOs`, `Dtos`, `Interfaces`, `Repositories`, `Helpers`, `Models`, `Entities`, or `Contracts`. `Apis` and `Services` appear only as direct children of a feature folder. Subfolders of `Data/` are allowed (DATA-16). |
| DOC-02 | `appsettings.json` and `appsettings.Production.json` do not set `OpenApi:Enabled` to `true`. `Program.cs` maps OpenAPI only inside the `IsDevelopment() || OpenApi:Enabled` condition (text check). |
| STR-06, DOC-01 | API `.csproj` has no `PackageReference` to `Swashbuckle.*`, `MediatR`, `AutoMapper`, or `FluentValidation*`, and has `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore`. |
| TEST-01 | Test `.csproj` references `Testcontainers.PostgreSql` and `Microsoft.AspNetCore.Mvc.Testing`, and not `Microsoft.EntityFrameworkCore.InMemory` or `Microsoft.EntityFrameworkCore.Sqlite`. |
| PRG-02 | Enforced by compilation: tests use `WebApplicationFactory<Program>`. |

### 4.7 Behavior tests

| Rule | Check |
|---|---|
| AUTH-06 | For every `/api` GET endpoint that requires a policy, call it anonymously (route values filled with `1` or a v7 Guid by constraint). Expect 401, never 302. Call one with a user lacking the permission, expect 403. |
| ERR-01 | `GET /api/does-not-exist` returns 404 with `application/problem+json`. A request that fails validation returns 400 `application/problem+json` with `errors`. Stack traces never appear in Production 500 bodies (review: needs a throwing test endpoint, which is awkward to add from `WebApplicationFactory`). |
| API-09 | `POST /api/notes` returns 201 with a `Location` header that resolves with GET (reference.md, `Create_returns_201_with_location`). |
| DOC-02 | Under `UseEnvironment("Production")`, `GET /openapi/v1.json` returns 404 without `OpenApi:Enabled` and 200 with `OpenApi:Enabled=true` (reference.md, `OpenApiExposureTests`). |

### 4.8 Source scans (Roslyn)

Syntax-only scans of the `.cs` files, so they see folders and `using` directives, which reflection cannot. Package: `Microsoft.CodeAnalysis.CSharp` 5.9.0 in the test project. No compilation or semantic model is needed.

| Rule | Check |
|---|---|
| STR-03, STR-07 | Every file under a top-level folder of the API project declares exactly `{Root}.{Folder}`. Project-root files (`Program.cs`, global usings) and EF-generated files in `Data/Migrations/` are skipped. |
| MOD-11, MOD-13 | The feature graph has no cycle. Feature A depends on feature B when a file in `A/` has a qualified name starting with `{Root}.B` (a `using`, a parameter, a call) or names a type declared under `B/` that A does not declare itself. `Program.cs`, `Infrastructure/`, `Data/`, and `Shared/` are not part of the graph. |
| MOD-09, MOD-13 | No file outside `Reports/` depends on `Reports/`, by the same measure. Project-root files compose every feature and are skipped. |
| DATA-02, MOD-13 | Every file in `Data/` (outside `Migrations/`) declares a top-level type named after the file (the part before the first dot, so `AppDbContext.Sets.cs` counts as `AppDbContext`), and every `IEntityTypeConfiguration<T>` sits in `T.cs`. |
| DATA-14 | No `QueryExpressionSyntax` (`from ... select`) in any `.cs` file under the repo root. |

```csharp
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed record SourceFile(string Path, CompilationUnitSyntax Root)
{
    public string? TopFolder => Path.IndexOf('/') is var i and > 0 ? Path[..i] : null;

    public bool IsGenerated => Path.StartsWith("Data/Migrations/", StringComparison.Ordinal);
}

internal static class Source
{
    public static readonly string RepoRoot = FindRepoRoot();
    public static readonly string ApiDir = System.IO.Path.Combine(RepoRoot, "src", Arch.Root);

    public static IReadOnlyList<SourceFile> ApiFiles { get; } = Parse(ApiDir);

    public static IReadOnlyList<SourceFile> Parse(string dir) => Directory
        .EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
        .Select(p => System.IO.Path.GetRelativePath(dir, p).Replace('\\', '/'))
        .Where(p => !p.Split('/').Any(s => s is "bin" or "obj"))
        .Select(p => new SourceFile(p, CSharpSyntaxTree.ParseText(File.ReadAllText(System.IO.Path.Combine(dir, p))).GetCompilationUnitRoot()))
        .ToArray();

    private static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (d.EnumerateFiles("*.sln").Any() || d.EnumerateFiles("*.slnx").Any()) return d.FullName;
        throw new InvalidOperationException("No .sln or .slnx above the test output directory.");
    }
}

internal static class FolderGraph
{
    private static readonly Dictionary<string, HashSet<string>> Declared = Source.ApiFiles
        .Where(f => f.TopFolder is not null)
        .GroupBy(f => f.TopFolder!)
        .ToDictionary(g => g.Key, g => g
            .SelectMany(f => f.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            .Select(t => t.Identifier.Text)
            .ToHashSet());

    public static bool IsFeature(string? folder) => folder is not null && !Arch.Layers.Contains(folder);

    // Top-level folders a file uses: qualified names ({Root}.X... in usings, parameters, or calls),
    // plus type names declared only under X/.
    public static IEnumerable<string> DependenciesOf(SourceFile f)
    {
        HashSet<string> own = f.TopFolder is { } top && Declared.TryGetValue(top, out var names) ? names : [];
        var prefix = Arch.Root + ".";
        var qualified = f.Root.DescendantNodes()
            .Where(n => n is QualifiedNameSyntax or MemberAccessExpressionSyntax)
            .Select(n => n.ToString())
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => n[prefix.Length..].Split('.')[0]);
        var used = f.Root.DescendantNodes().OfType<SimpleNameSyntax>()
            .Where(IsTypePosition)
            .Select(n => n.Identifier.Text)
            .Where(n => !own.Contains(n))
            .ToHashSet();
        var byName = Declared.Where(d => d.Value.Overlaps(used)).Select(d => d.Key);
        return qualified.Concat(byName).Where(d => d != f.TopFolder).Distinct();
    }

    // Skip member names: x.Name, { Name = ... } initializers, and Name: arguments.
    private static bool IsTypePosition(SimpleNameSyntax n) => n.Parent switch
    {
        MemberAccessExpressionSyntax m => m.Expression == n,
        NameColonSyntax => false,
        AssignmentExpressionSyntax a => !(a.Left == n && a.Parent is InitializerExpressionSyntax),
        _ => true,
    };
}
```

```csharp
public class SourceTests
{
    [Fact]
    public void No_feature_dependency_cycles()
    {
        var edges = Source.ApiFiles
            .Where(f => FolderGraph.IsFeature(f.TopFolder))
            .SelectMany(f => FolderGraph.DependenciesOf(f).Where(FolderGraph.IsFeature).Select(d => (From: f.TopFolder!, To: d)))
            .Distinct()
            .ToLookup(e => e.From, e => e.To);

        var cycles = new List<string>();
        var done = new HashSet<string>();
        void Visit(string node, List<string> path)
        {
            if (path.IndexOf(node) is var i and >= 0) { cycles.Add(string.Join(" -> ", path[i..].Append(node))); return; }
            if (!done.Add(node)) return;
            path.Add(node);
            foreach (var next in edges[node]) Visit(next, path);
            path.RemoveAt(path.Count - 1);
        }
        foreach (var feature in edges.Select(g => g.Key).Order()) Visit(feature, []);

        Assert.True(cycles.Count == 0, string.Join(Environment.NewLine, cycles));
    }

    // Program.cs and other project-root files compose every feature, so they are skipped.
    [Fact]
    public void Nothing_depends_on_reports()
    {
        var failures = Source.ApiFiles
            .Where(f => f.TopFolder is not null && f.TopFolder != "Reports")
            .Where(f => FolderGraph.DependenciesOf(f).Contains("Reports"))
            .Select(f => f.Path)
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Namespace_matches_top_level_folder()
    {
        var failures = Source.ApiFiles
            .Where(f => f.TopFolder is not null && !f.IsGenerated)
            .SelectMany(f => f.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
                .Select(n => n.Name.ToString())
                .DefaultIfEmpty("(global)")
                .Where(ns => ns != $"{Arch.Root}.{f.TopFolder}")
                .Select(ns => $"{f.Path}: {ns}, expected {Arch.Root}.{f.TopFolder}"))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Data_files_are_named_after_their_type()
    {
        var failures = new List<string>();
        foreach (var f in Source.ApiFiles.Where(f => f.TopFolder == "Data" && !f.IsGenerated))
        {
            var name = System.IO.Path.GetFileName(f.Path).Split('.')[0];
            var types = f.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                .ToArray();
            if (!types.Any(t => t.Identifier.Text == name))
                failures.Add($"{f.Path}: declares no type named {name}");

            var configured = types
                .SelectMany(t => t.BaseList is { } list ? list.Types.Select(b => b.Type) : [])
                .OfType<GenericNameSyntax>()
                .Where(g => g.Identifier.Text == "IEntityTypeConfiguration")
                .Select(g => g.TypeArgumentList.Arguments[0].ToString());
            failures.AddRange(configured.Where(c => c != name).Select(c => $"{f.Path}: configuration for {c} belongs in {c}.cs"));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Queries_use_method_syntax()
    {
        var failures = Source.Parse(Source.RepoRoot)
            .SelectMany(f => f.Root.DescendantNodes().OfType<QueryExpressionSyntax>()
                .Select(q => $"{f.Path}:{q.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }
}
```

The dependency scan matches simple names, so it has limits: a member named like another feature's type is skipped only in `x.Name`, `{ Name = ... }` initializers, and `Name:` arguments; a type name declared in two features counts for both; and a value reached through `var` is not named, so only the call that produced it counts. Keep type names unique across features. `.Join` (DATA-14) stays with review, because `string.Join` shares the name.

### 4.9 Optional: project-specific write-ownership scan

MOD-05 is a review rule by default. For a property whose changes must stay in one feature (a balance field only its owning feature may change), add a scan like this with the project's own names. It flags assignments, compound assignments, `++`/`--`, object initializers, and `ExecuteUpdate`'s `SetProperty(x => x.Property, ...)` outside the owner. `Data/` and the layers are not scanned.

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class WriteOwnershipTests
{
    private const string Owner = "FeatureA";
    private const string Property = "Balance";

    // Files that assign a different member with the same name. Each entry says why.
    private static readonly HashSet<string> Allowed =
    [
        // "FeatureB/SomeApi.cs", // SomeResponse.Balance is a DTO property, not EntityA.Balance
    ];

    [Fact]
    public void Only_the_owner_changes_EntityA_balance()
    {
        var failures = Source.ApiFiles
            .Where(f => FolderGraph.IsFeature(f.TopFolder) && f.TopFolder != Owner && !Allowed.Contains(f.Path))
            .SelectMany(f => f.Root.DescendantNodes()
                .Where(Writes)
                .Select(n => $"{f.Path}:{n.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    // Assignments (=, +=, ...), ++/--, and ExecuteUpdate's SetProperty(x => x.Balance, ...).
    private static bool Writes(SyntaxNode n) => n switch
    {
        AssignmentExpressionSyntax a => Target(a.Left) == Property,
        PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.PreIncrementExpression) || u.IsKind(SyntaxKind.PreDecrementExpression)
            => Target(u.Operand) == Property,
        PostfixUnaryExpressionSyntax u => Target(u.Operand) == Property,
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "SetProperty" } } i
            => i.ArgumentList.Arguments.FirstOrDefault()?.Expression is SimpleLambdaExpressionSyntax { Body: ExpressionSyntax body }
               && Target(body) == Property,
        _ => false,
    };

    private static string? Target(ExpressionSyntax e) => e switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        IdentifierNameSyntax i => i.Identifier.Text,
        _ => null,
    };
}
```

It matches by member name, not by type, so a DTO with a same-named property is a false positive: add that file to the allowlist with a reason, or rename the DTO property.

Verified on 2026-09-27 with SDK 10.0.401, Microsoft.CodeAnalysis.CSharp 5.9.0, NetArchTest.Rules 1.3.2, and Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 in a scratch solution with this layout (the reference.md `Note`, `AppUser`, `DbTransactions`, `NoteService`, and navigation-based report query compiled, and the report query translated to SQL). Each test in 4.1 (layers), 4.8, and 4.9 failed on a deliberate violation and passed once it was fixed: `Data/` and `Shared/` referencing a feature or `Data`, a `FeatureA -> FeatureB -> FeatureA` loop (through a fully qualified call, a `using`, and a global using), a feature using a `Reports/` type, a `Models`-style sub-namespace, a `Data/` file named after no type with a misplaced configuration, a query expression, and a `Balance` write through `-=`, an initializer, and `SetProperty` outside the owner. A one-way `FeatureB -> FeatureA` call, `Program.cs` registering `Reports`, and `{Root}.Data.Migrations` did not fail.

## 5. Rule map

| Rule | Check | Rule | Check |
|---|---|---|---|
| STR-01 | 4.6 | AUTH-03 | review |
| STR-02 | 4.6 | AUTH-04 | review |
| STR-03 | 4.8 | AUTH-05 | review |
| STR-04 | 4.6 | AUTH-06 | 4.7 |
| STR-05 | ban, 4.1 | AUTH-07 | 4.2, 4.3 |
| STR-06 | 4.6 | AUTH-08 | review |
| STR-07 | 4.8 | DATA-01 | review |
| MOD-01 | 4.2 | DATA-02 | 4.5, 4.8, ban (mapping attributes) |
| MOD-02 | review | DATA-03 | 4.2 |
| MOD-03 | 4.1, 4.8 | DATA-04 | review (4.5 reports) |
| MOD-04 | review, 4.8 (query syntax) | DATA-05 | ban `Guid.NewGuid`, 4.5 |
| MOD-05 | review, optional 4.9 | DATA-06 | ban (SaveChanges, Migrate), review (sync LINQ) |
| MOD-06 | 4.1 | DATA-07 | review |
| MOD-07 | review (subfolders allowed by 4.6) | DATA-08 | ban (DateTime/DateTimeOffset now) |
| MOD-08 | review | DATA-09 | ban (*Raw) |
| MOD-09 | 4.8 | DATA-10 | ban (EnsureCreated), review |
| MOD-10 | review | DATA-11 | review |
| MOD-11 | 4.8 | DATA-12 | review |
| MOD-12 | 4.8 (cycles), review (fix order) | DATA-13 | 4.2 |
| MOD-13 | 4.1, 4.8 | DATA-14 | 4.8, review (`.Join`) |
| PRG-01 | review | DATA-15 | review |
| PRG-02 | compile | DATA-16 | review (subfolders allowed by 4.6) |
| API-01 | 4.2, 4.3 | CODE-01 | CA1822 |
| API-02 | 4.3 (lambdas), review | CODE-02 | review |
| API-03 | ban `Results`, 4.3 | CODE-03 | 4.2 |
| API-04 | review (ASP0018/22/23 help) | CODE-04 | 4.2, review |
| API-05 | 4.3 | CODE-05 | 4.1 |
| API-06 | 4.3 | DI-01 | review |
| API-07 | 4.3 | DI-02 | 4.4 (ValidateScopes) |
| API-08 | CA2016 | DI-03 | 4.4 |
| API-09 | 4.3, 4.7 (creates), review | DI-04 | 4.2 |
| API-10 | review | DI-05 | ban `HttpClient` ctors |
| DTO-01 | review | DI-06 | 4.4 |
| DTO-02 | 4.3 | DI-07 | ban, ASP0000 |
| DTO-03 | 4.3 | DI-08 | review |
| DTO-04 | review | SEED-01 | 4.2, 4.4 |
| DTO-05 | 4.3 | SEED-02 | review |
| DTO-06 | review | SEED-03 | review |
| ERR-01 | 4.2, 4.7 | DOC-01 | 4.6 |
| ERR-02 | review | DOC-02 | 4.6, 4.7 |
| ERR-03 | 4.3 (report), review | DOC-03 | review |
| ERR-04 | review | TEST-01 | 4.6 |
| ERR-05 | 4.2, review | TEST-02 | review |
| ERR-06 | review | TEST-03 | review |
| AUTH-01 | review | TEST-04 | review |
| AUTH-02 | 4.2 | TEST-05 | CI runs the test project |

## 6. Review-only rules

These need judgment. Agents check them against the diff and list any deviation in the PR description.

- Design: MOD-02, MOD-04 (`.Join`), MOD-05 (unless 4.9 is added), MOD-07, MOD-08, MOD-10, MOD-12 (fix order), PRG-01, API-02 (beyond the lambda check), API-04, API-09, API-10, CODE-02, DI-01.
- Data: DATA-01, DATA-04, DATA-06 (sync LINQ), DATA-07, DATA-14 (`.Join`), DATA-15, DATA-16, DATA-10, DATA-11, DATA-12, DI-08.
- Errors and security: ERR-02, ERR-03 (beyond the report), ERR-04, ERR-05 (throw sites), ERR-06, AUTH-01, AUTH-03 to AUTH-05, AUTH-08.
- DTOs and docs: DTO-01, DTO-04, DTO-06, DOC-03.
- Seeding and tests: SEED-02, SEED-03, TEST-02 to TEST-04.
- CODE-04 in part: whether a `static readonly` value of a custom type is truly immutable.

Candidates to automate later: DATA-01 (Roslyn check that `OnModelCreating` has only the two calls), DI-08 (a custom analyzer flagging `Task.WhenAll` with arguments that share a `DbContext`), and API-04 (a route-pattern regex test).
