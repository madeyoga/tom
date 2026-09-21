# Tom.WebApi

Opinionated ASP.NET Core 10 Web API. PostgreSQL, Identity with `Guid` keys (UUID v7 on create), AuthEndpoints cookie sign-in, OpenAPI and Scalar. Domain entities can keep `long` PKs.

Pair with [`tom-admin`](../tom-admin) for a Nuxt cookie SPA (register, login, 2FA, passkeys, security, step-up).

## Run

1. Postgres on `localhost:5432` with database `tom_webapi` (user/password `postgres`), or change `ConnectionStrings:DefaultConnection` in `appsettings.json`.
2. Copy `.env.example` to `.env` and set `Seed__Password` (`.env` is gitignored). Development loads `.env` only.
3. `dotnet tool restore`
4. `dotnet run` (Development applies migrations, then seeds `Seed__AdminEmail` if `Seed__Password` is set)

Or apply the schema yourself: `dotnet ef database update`.

The HTTP profile listens on `http://localhost:5080`.

- Health: `GET /health`
- OpenAPI: `/openapi/v1.json`
- Scalar: `/scalar`
- CSRF: `GET /identity/csrfToken` (header name `RequestVerificationToken`)
- Passkeys: `/account/passkeys/*`

There is no resource under `/api` yet. Add `api.MapXApi()` calls on the `api` group in `Program.cs`.

## Pair with tom-admin

Development is a **separate-origin** cookie SPA:

1. Run this API on `:5080`.
2. Create the admin with `npx nuxi@latest init -t gh:madeyoga/tom/templates/tom-admin` and run it on `:3000` with `NUXT_PUBLIC_API_BASE=http://localhost:5080`.
3. Development CORS policy `Frontend` uses `Frontend:Origin` (default `http://localhost:3000`) and `AllowCredentials()`. Production leaves CORS off; put the SPA behind a reverse proxy on the same origin.

Email confirmation redirects to `http://localhost:3000/confirm-email` (`AuthEndpoints:EmailConfirmation:ConfirmEmailRedirectUri`). Allowed origins include `http://localhost:3000`.

## Auth

Cookie login is AuthEndpoints `LoginCookie` at `POST /identity/login`. `?useCookies=true` does nothing. Persistent cookie: `?useSessionCookies=false`.

JWT is off. Passkeys are on (`AuthEndpoints:Passkeys:Enabled`, `ServerDomain` `localhost`). Identity keys are `Guid`. Password and seed users get UUID v7 from `AppUser`/`AppRole` constructors. AuthEndpoints passkey register still assigns `Guid.NewGuid()` (v4) for those users.

`FileEmailSender` writes Identity mail to `emails/` and the log. Replace it with SMTP before Production. DataProtection keys live in `keys/` (gitignored).

Production one-shot Admin: set `Seed__BootstrapAdminEmail` and `Seed__BootstrapAdminPassword` in the environment (not `.env`) when no Admin exists. `POST /identity/register` does not assign roles. Development never uses those bootstrap keys.

## Host choices

Development CORS is for the paired Nuxt origin only. Do not set `SameSite=None` / `Secure` on HTTP localhost cookies. Production: same-origin reverse proxy, no CORS.

No tenant filters. No `AuthenticatedUserService`. Handlers that need the signed-in user can take `ClaimsPrincipal` or `UserManager<AppUser>`.
