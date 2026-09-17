# Tom.WebApi

Opinionated ASP.NET Core 10 Web API. PostgreSQL, Identity with `Guid` keys (UUID v7 on create), AuthEndpoints cookie sign-in, OpenAPI and Scalar. Domain entities can keep `long` PKs.

## Run

1. Postgres on `localhost:5432` with database `tom_webapi` (user/password `postgres`), or change `ConnectionStrings:DefaultConnection` in `appsettings.json`.
2. Copy `.env.example` to `.env` and set `Seed__Password` (`.env` is gitignored). Development loads `.env` only.
3. `dotnet tool restore`
4. `dotnet run` (Development applies migrations, then seeds `Seed__AdminEmail` if `Seed__Password` is set)

Or apply the schema yourself: `dotnet ef database update`.

- Health: `GET /health`
- OpenAPI: `/openapi/v1.json`
- Scalar: `/scalar`
- CSRF: `GET /identity/csrfToken` (header name `RequestVerificationToken`)

There is no resource under `/api` yet. Add `api.MapXApi()` calls on the `api` group in `Program.cs`.

## Auth

Cookie login is AuthEndpoints `LoginCookie` at `POST /identity/login`. `?useCookies=true` does nothing. Persistent cookie: `?useSessionCookies=false`.

JWT is off. Passkeys are off (`AuthEndpoints:Passkeys:Enabled`). Identity keys are `Guid`, so you can turn passkeys on later. Password and seed users get UUID v7 from `AppUser`/`AppRole` constructors. AuthEndpoints passkey register still assigns `Guid.NewGuid()` (v4) for those users.

`FileEmailSender` writes Identity mail to `emails/` and the log. Replace it with SMTP before Production. DataProtection keys live in `keys/` (gitignored).

Production one-shot Admin: set `Seed__BootstrapAdminEmail` and `Seed__BootstrapAdminPassword` in the environment (not `.env`) when no Admin exists. `POST /identity/register` does not assign roles. Development never uses those bootstrap keys.

## Host choices

Same-origin cookies. No CORS. If the SPA is on another origin, add a CORS policy with credentials yourself.

No tenant filters. No `AuthenticatedUserService`. Handlers that need the signed-in user can take `ClaimsPrincipal` or `UserManager<AppUser>`.
