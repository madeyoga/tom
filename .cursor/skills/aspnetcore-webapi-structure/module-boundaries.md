# Module boundaries

Rules for what one module may use from another. IDs match SKILL.md. Checks are in enforceable-checks.md.

## How the tests find a module

The architecture tests match a module by its namespace prefix `{App}.Api.{Module}`, including every subnamespace. Subfolders from a MOD-07 split (`{Module}/Models/`, `{Module}/Apis/`, `{Module}/Services/`) count as part of the module. The Contracts exception matches `{App}.Api.{Module}.Contracts` and its subnamespaces only, so a folder called `Contracts` deeper inside a module is not a public surface.

## Why tests, not `internal`

All modules share one assembly, so `internal` does not stop module A from using module B's types. Mark entities, configurations, services, and endpoint DTOs `internal` where the compiler allows it. That keeps them out of the public surface and out of test-assembly reach. The actual boundary is enforced by architecture tests (MOD-03, MOD-04, DATA-03).

Endpoint classes and HTTP DTOs can stay `public`. Minimal API handlers, System.Text.Json, and OpenAPI work with them either way, and public is less friction with the validation source generator.

## The three kinds of folder

| Folder | Contains | May depend on |
|---|---|---|
| `Shared/` | Shared kernel: `PaginatedItems<T>`, `PaginationRequest`, `Problems`, `Result<T>`/`Result`, `CurrentUser`, `IDataSeeder`, optional `IAuditable` | BCL, ASP.NET Core, EF Core abstractions. No module. |
| `Infrastructure/` | `AppDbContext`, `AddInfrastructure`, auth setup, OpenAPI setup, `DatabaseInitializer`, optional `AuditableInterceptor` | `Shared/`, and module types only where it must compose them (`AppDbContext` inherits the Identity module's `AppUser`/`AppRole`) |
| `{Module}/` | Entities, configurations, endpoints, services, seeders, permissions, `Contracts/` | `Shared/`, `Infrastructure/` (`AppDbContext` only), other modules' `Contracts/` |

Keep `Shared/` small. Add a type there only when at least two modules need it and it has no business meaning. A "Customer" or "Money with currency rules" type belongs to a module, not to `Shared/`.

No base entity class. EF Core does not need one, and a base class tends to collect behavior that belongs to one module. If several entities need timestamps, implement `Shared/IAuditable` (`CreatedAt`, `UpdatedAt`) and let `Infrastructure/AuditableInterceptor` set them from `TimeProvider`.

## What a module may use from another module (MOD-03)

Allowed:

- Types in `{App}.Api.{B}.Contracts`: public records (`NoteSummary`), public enums, and one or more public concrete classes (`NotesQueries`, `NotesCommands`).
- Permission constants in `{B}Permissions`, when module A's endpoint guards data that B owns. Prefer A's own permission.
- IDs of B's rows (`long NoteId`, `Guid UserId`), stored as plain columns.

Not allowed:

- B's entities, `IEntityTypeConfiguration` classes, `db.B()` set accessors, services, seeders, or HTTP DTOs.
- Queries on B's tables through `db.Set<BEntity>()` or raw SQL.
- Writes to B's tables in any form.

Contracts classes are concrete (no interface per service, DATA-13). Add an interface in `Contracts/` only when there is a real second implementation, for example a module that can be switched off and replaced by a no-op.

Contracts return Contracts records, never entities. A Contracts method that can fail returns `Result<T>` or `Result` from `Shared/` (ERR-04), never throws. A Contracts class may use its own module's internals in its body.

## Cross-module relationships (MOD-04)

- Store the other module's key as a scalar: `public Guid OwnerUserId { get; set; }`. No navigation property to `AppUser` or to any other module's entity.
- Keep the database FK constraint by default. Declare it in the referencing module's configuration without a navigation:

  ```csharp
  builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
  ```

  This line is the only permitted reference to another module's entity type. The architecture test allows it only inside `IEntityTypeConfiguration<T>` classes.
- To show data from another module, query your rows, collect the foreign IDs, and call B's Contracts class once for all of them (`UserDirectory.GetDisplayNamesAsync(ids, ct)`). No N+1 calls, no joins across module tables in LINQ.
- Reports that must join across many modules are their own module (`Reports/`) and use read-only SQL views or Contracts queries. Document each such view in the module that owns the underlying tables.

## Transactions across modules

`AppDbContext` is scoped, so every module in one request shares the same instance.

- A Contracts write method saves its own changes (`SaveChangesAsync`).
- When one use case must change two modules atomically, the orchestrating handler or service opens the transaction:

  ```csharp
  await using var tx = await db.Database.BeginTransactionAsync(ct);
  var archived = await notesCommands.ArchiveAsync(noteId, userId, ct);
  if (archived.Problem is { } problem) return problem;        // tx disposes without commit = rollback
  var recorded = await auditCommands.RecordAsync(..., ct);
  if (recorded.Problem is { } auditProblem) return auditProblem;
  await tx.CommitAsync(ct);
  return TypedResults.NoContent();
  ```

  If `EnableRetryOnFailure` is on, wrap this in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`.

## Direct calls vs events

- Default: direct calls to the other module's Contracts class. They are visible, debuggable, and testable.
- Add in-process events only when a module must react to another module without the publisher knowing about it (for example several modules clean up when a user is deleted). Then add a minimal `IEventPublisher` and `IEventHandler<TEvent>` pair in `Shared/`. Handlers run in the same request and scope, after the publisher saves. Event records live in the publisher's `Contracts/`.
- No MediatR, message bus, or outbox until there is a second process that needs it.

## DbContext layout

- One `AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>` in `Infrastructure/Data/`.
- `OnModelCreating`: `base.OnModelCreating(builder)` then `builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`. `ApplyConfigurationsFromAssembly` also finds `internal` configuration classes that have a parameterless constructor.
- No module `DbSet` properties on the context (DATA-03). Each module has:

  ```csharp
  internal static class NotesDb
  {
      public static DbSet<Note> Notes(this AppDbContext db) => db.Set<Note>();
  }
  ```

- Optional schema per module. Pick it for the whole project, not per module. Set it in each configuration with `builder.ToTable("notes", NotesModule.Schema)`. Identity tables stay in the default schema. Migrations stay in one `Migrations/` folder and one migrations history table.

## Identity as a module

`Identity/` owns `AppUser`, `AppRole`, the users API, role seeding, and `Identity/Contracts/UserDirectory` (display names, existence checks). Other modules never inject `UserManager<AppUser>` or read `AppUser` directly. `Shared/CurrentUser` exposes only `Guid? UserId` and permission checks from claims.

## Adding a module

1. Create `{Module}/` with `{Module}Module.cs` (in the module's root namespace), `{Module}Permissions.cs`, and an empty `Contracts/` only if another module needs something.
2. Add entities with configurations and the `{Module}Db` accessor.
3. Add `{Resource}Api.cs` files and call them from `Map{Module}Module`.
4. Call `Add{Module}Module` and `Map{Module}Module` in `Program.cs`.
5. Split into `Models/`, `Apis/`, `Services/` later only when MOD-07 says so. Namespaces follow the folders.
6. Run the architecture tests. The module list is discovered from top-level namespaces, so a new module is checked without editing the tests.
