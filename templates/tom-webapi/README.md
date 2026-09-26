# Tom.WebApi

Opinionated ASP.NET Core 10 Web API solution. PostgreSQL, Identity with `Guid` keys (UUID v7 on create, including passkey sign-up), AuthEndpoints cookie sign-in, OpenAPI and Scalar in Development. Domain entities keep `long` primary keys. The `Notes` folder is a small example module; delete it and the `AddNotesModule` / `MapNotesModule` calls in `Program.cs` when you add a real feature.

```text
src/Tom.WebApi.Api/          # the API
tests/Tom.WebApi.Api.Tests/  # Testcontainers PostgreSQL, endpoint tests, architecture tests
```

`dotnet new` replaces `Tom.WebApi` in those paths. Pair with [`tom-admin`](../tom-admin) for a Nuxt cookie SPA (register, login, 2FA, passkeys, security, step-up).

## Run

1. Postgres on `localhost:5432` with database `tom_webapi` (user/password `postgres`), or change `ConnectionStrings:DefaultConnection` in `src/Tom.WebApi.Api/appsettings.json`.
2. From this directory, copy `.env.example` to `.env` and set `Seed__Password` (`.env` is gitignored). Development loads `.env` from the current directory only.
3. `dotnet tool restore`
4. `dotnet run --project src/Tom.WebApi.Api` (Development applies migrations, then seeds `Seed__AdminEmail` if `Seed__Password` is set)

Or apply the schema yourself: `dotnet ef database update --project src/Tom.WebApi.Api`.

The HTTP profile listens on `http://localhost:5080`.

- Health: `GET /health`
- OpenAPI: `/openapi/v1.json` (Development, or `OpenApi__Enabled=true`)
- Scalar: `/scalar` (same condition)
- CSRF: `GET /identity/csrfToken` (header name `RequestVerificationToken`)
- Passkeys: `/account/passkeys/*`
- Example notes: `GET /api/notes`, `POST /api/notes` (Admin role, antiforgery on writes)

## Tests

`dotnet test` starts PostgreSQL with Testcontainers and runs endpoint tests plus the architecture checks. `dotnet format --verify-no-changes` is the formatting gate. `.github/workflows/ci.yml` restores, builds Release, tests, and formats, then a final job named `ci`.

## Pair with tom-admin

Development is a **separate-origin** cookie SPA:

1. Run this API on `:5080`.
2. Create the admin with `npx nuxi@latest init -t gh:madeyoga/tom/templates/tom-admin` and run it on `:3000` with `NUXT_PUBLIC_API_BASE=http://localhost:5080`.
3. Development CORS policy `Frontend` uses `Frontend:Origin` (default `http://localhost:3000`) and `AllowCredentials()`. Production leaves CORS off.

Email confirmation redirects to `http://localhost:3000/confirm-email` (`AuthEndpoints:EmailConfirmation:ConfirmEmailRedirectUri`). Allowed origins include `http://localhost:3000`.

## Same-origin production

[`examples/same-origin`](../../examples/same-origin) is one public host. Caddy sends `/identity*`, `/account*` (passkeys), `/auth*` (external logins), `/api*`, `/health`, `/scalar*`, and `/openapi*` to this API, and every other path to Nuxt. `/api/confirm-email` stays on Nuxt: it is the confirm-email helper, not an API proxy. The browser uses a relative API base, so cookies are host-only. Do not set `SameSite=None`.

`Dockerfile` is multi-stage (`sdk` then `aspnet`). The image listens on `8080` (`ASPNETCORE_URLS=http://+:8080`). TLS ends at Caddy. The entrypoint chowns the `keys/` and `emails/` mounts, then drops to the non-root `app` user. `dotnet new` replaces the `Tom.WebApi` source name in that file. `src/Tom.WebApi.Api/Migrations` contains `Initial`. Development `dotnet run` applies it on `:5080`. The example compose `migrate` stage runs `dotnet ef database update` before the API starts. Add a new migration when the model changes: `dotnet ef migrations add {Name} --project src/Tom.WebApi.Api --output-dir Migrations`. The API does not apply migrations itself in Production.

Production environment (real variables, not `.env`):

- `AuthEndpoints__Passkeys__ServerDomain` is the public host (`localhost` or `app.example.com`), never the container name.
- `AuthEndpoints__EmailConfirmation__ConfirmEmailRedirectUri` is `https://{host}/confirm-email`. Production rejects an `http` redirect.
- `AuthEndpoints__EmailConfirmation__AllowedRedirectOrigins__0` is that same `https` origin.
- `Seed__BootstrapAdminEmail` and `Seed__BootstrapAdminPassword` create the first Admin when none exists.
- Leave `OpenApi__Enabled` unset. Set it to `true` only on a test or staging host that runs with `ASPNETCORE_ENVIRONMENT=Production`.

Production enables `ForwardedHeaders` for `X-Forwarded-For` and `X-Forwarded-Proto` only, and does not register CORS. DataProtection keys stay in `keys/` under the content root (`SetApplicationName` follows the app name). Mount a volume there. Compose does that.

## Auth

Cookie login is AuthEndpoints `LoginCookie` at `POST /identity/login`. `?useCookies=true` does nothing. Persistent cookie: `?useSessionCookies=false`.

JWT is off. Passkeys are on (`AuthEndpoints:Passkeys:Enabled`, `ServerDomain` `localhost`). Identity keys are `Guid`. `AppUser` and `AppRole` set `Id = Guid.CreateVersion7()`. Passkey sign-up uses `AddPasskeyUserIdFactory` so those ids are v7 as well. Configurations mark the ids `ValueGeneratedNever()`.

`FileEmailSender` writes Identity mail to `emails/` and the log, using `TimeProvider` for the file name. Replace it with SMTP before Production. DataProtection keys live in `keys/` (gitignored).

Production one-shot Admin: set `Seed__BootstrapAdminEmail` and `Seed__BootstrapAdminPassword` in the environment (not `.env`) when no Admin exists. `POST /identity/register` does not assign roles. Development never uses those bootstrap keys when `Seed__Password` is set; without it, Development logs a warning and skips the Admin user.

## Host choices

Development CORS is for the paired Nuxt origin only. Do not set `SameSite=None` on HTTP localhost cookies. Production: the Caddy path-split above, CORS off, cookie host-only with the framework default SameSite. Nuxt in the example stays Node SSR. Serving a `nuxt generate` build from Caddy `file_server` is optional and documented only in the example README.

No tenant filters. Handlers read the signed-in user from `Shared/CurrentUser` (claims only). `ValidateScopes` and `ValidateOnBuild` are on in every environment. OpenAPI and Scalar are mapped only in Development or when `OpenApi:Enabled` is true. That flag is not set in `appsettings.json`.
