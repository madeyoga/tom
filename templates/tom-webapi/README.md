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
3. Development CORS policy `Frontend` uses `Frontend:Origin` (default `http://localhost:3000`) and `AllowCredentials()`. Production leaves CORS off.

Email confirmation redirects to `http://localhost:3000/confirm-email` (`AuthEndpoints:EmailConfirmation:ConfirmEmailRedirectUri`). Allowed origins include `http://localhost:3000`.

## Same-origin production

[`examples/same-origin`](../../examples/same-origin) is one public host. Caddy sends `/identity*`, `/account*` (passkeys), `/auth*` (external logins), `/api*`, `/health`, `/scalar*`, and `/openapi*` to this API, and every other path to Nuxt. `/api/confirm-email` stays on Nuxt: it is the confirm-email helper, not an API proxy. The browser uses a relative API base, so cookies are host-only. Do not set `SameSite=None`.

`Dockerfile` is multi-stage (`sdk` then `aspnet`). The image listens on `8080` (`ASPNETCORE_URLS=http://+:8080`). TLS ends at Caddy. The entrypoint chowns the `keys/` and `emails/` mounts, then drops to the non-root `app` user. `dotnet new` replaces the `Tom.WebApi` source name in that file. A `migrate` stage runs `dotnet ef database update` for the example compose file; this template does not ship a migration, so add one (`dotnet ef migrations add InitialIdentity`) before that stack can seed. Development `dotnet run` still applies migrations on `:5080`.

Production environment (real variables, not `.env`):

- `AuthEndpoints__Passkeys__ServerDomain` is the public host (`localhost` or `app.example.com`), never the container name.
- `AuthEndpoints__EmailConfirmation__ConfirmEmailRedirectUri` is `{origin}/confirm-email`.
- `AuthEndpoints__EmailConfirmation__AllowedRedirectOrigins__0` is that same origin.
- `Seed__BootstrapAdminEmail` and `Seed__BootstrapAdminPassword` create the first Admin when none exists.

Production enables `ForwardedHeaders` for `X-Forwarded-For` and `X-Forwarded-Proto` only, and does not register CORS. DataProtection keys stay in `keys/` (`SetApplicationName` is unchanged). Mount a volume there. Compose does that.

## Auth

Cookie login is AuthEndpoints `LoginCookie` at `POST /identity/login`. `?useCookies=true` does nothing. Persistent cookie: `?useSessionCookies=false`.

JWT is off. Passkeys are on (`AuthEndpoints:Passkeys:Enabled`, `ServerDomain` `localhost`). Identity keys are `Guid`. Password and seed users get UUID v7 from `AppUser`/`AppRole` constructors. AuthEndpoints passkey register still assigns `Guid.NewGuid()` (v4) for those users.

`FileEmailSender` writes Identity mail to `emails/` and the log. Replace it with SMTP before Production. DataProtection keys live in `keys/` (gitignored).

Production one-shot Admin: set `Seed__BootstrapAdminEmail` and `Seed__BootstrapAdminPassword` in the environment (not `.env`) when no Admin exists. `POST /identity/register` does not assign roles. Development never uses those bootstrap keys.

## Host choices

Development CORS is for the paired Nuxt origin only. Do not set `SameSite=None` on HTTP localhost cookies. Production: the Caddy path-split above, CORS off, cookie host-only with the framework default SameSite. Nuxt in the example stays Node SSR. Serving a `nuxt generate` build from Caddy `file_server` is optional and documented only in the example README.

No tenant filters. No `AuthenticatedUserService`. Handlers that need the signed-in user can take `ClaimsPrincipal` or `UserManager<AppUser>`.
