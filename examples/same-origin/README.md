# Same-origin example

One public host. Caddy sends API paths to the Web API and every other path to the Nuxt Node server. The browser calls the API on that same host, so cookies stay host-only and the API leaves CORS off.

Day-to-day development is still two ports: Nuxt on `:3000`, the API on `:5080`, with the Development CORS policy. This stack does not replace that.

## Run

From this directory:

```text
cp .env.example .env
docker compose up --build
```

Open `https://localhost`. Caddy uses its internal CA for that name, so the browser warns until you trust the root:

```text
docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt ./caddy-root.crt
```

Trust `caddy-root.crt` in the OS or browser trust store. A DNS name in `PUBLIC_HOST` (for example `app.example.com`) makes Caddy obtain a public certificate instead. Point that name at this host, and set `PUBLIC_ORIGIN` to `https://` that name.

The confirm-email redirect must be `https` in Production. `PUBLIC_ORIGIN=http://...` or `PUBLIC_HOST=:80` will not boot the API.

The API image listens on `8080` inside the compose network. TLS ends at Caddy. `ASPNETCORE_URLS=http://+:8080`.

## What Caddy routes

| Path | Upstream |
| --- | --- |
| `/api/confirm-email` | Nuxt. This is the Nitro helper the check-email page calls. It is not a general API proxy. |
| `/identity*`, `/account*`, `/auth*`, `/api*`, `/health`, `/scalar*`, `/openapi*` | Web API |
| everything else | Nuxt `:3000` |

`/auth*` is the external-login prefix the admin kit already calls. `/account*` is passkeys.

The Web API proxy keeps the browser `Host` and Caddy's `X-Forwarded-For` / `X-Forwarded-Proto` headers. Production enables those two forwarded headers and does not enable CORS. Cookies stay host-only with the framework default SameSite. Do not set `SameSite=None`.

## Browser API base and confirm-email

`NUXT_PUBLIC_API_BASE=.` is the same-origin sentinel. Blank and `/` mean the same thing. Nuxt ignores an empty environment override and would keep `http://localhost:5080`, so this file uses `.`.

`credentials: 'include'` is unchanged. With a relative base, the browser sends the cookie to `/identity` and `/account` on the public host.

The confirm-email Nitro route runs inside the admin container, so it cannot use that relative base. `NUXT_API_INTERNAL=http://webapi:8080` is the server-side fetch. Dual-port dev leaves `NUXT_API_INTERNAL` unset and the route keeps using `http://localhost:5080`.

## Passkeys and email links

Set both of these to the public site, never to the `webapi` container name:

- `PASSKEYS_SERVER_DOMAIN` is the host only (`localhost` or `app.example.com`).
- `PUBLIC_ORIGIN` is the `https` origin (`https://localhost` or `https://app.example.com`). The API uses it as the confirm-email redirect and the only allowed redirect origin.

`https://localhost` is a secure context once you continue past or trust the internal certificate, so passkeys work there. Any other host needs a certificate the browser accepts. `PASSKEYS_SERVER_DOMAIN` stays the hostname only.

## Postgres, migrations, keys, bootstrap

Postgres data is the `postgres_data` volume. The host port defaults to `5432` (`POSTGRES_PORT`).

The API template includes an `InitialIdentity` migration. The `migrate` service runs `dotnet ef database update` against a fresh Postgres and exits. The API does not apply migrations itself outside Development.

To apply that migration from the host instead, with Postgres published on `POSTGRES_PORT`:

```text
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=tom_webapi;Username=postgres;Password=postgres" dotnet ef database update
```

DataProtection keys are the `webapi_keys` volume, mounted at `/app/keys`. The app still calls `SetApplicationName`. Keep that volume: a new empty key ring logs everyone out and breaks protected tokens. The container starts as root only to chown `keys/` and `emails/`, then drops to the non-root `app` user.

Identity mail still goes to `emails/` (bind-mounted here) and the API log until you replace `FileEmailSender` with SMTP.

Production does not read the API project's `.env`. The first Admin is a one-shot: set `SEED_BOOTSTRAP_ADMIN_EMAIL` and `SEED_BOOTSTRAP_ADMIN_PASSWORD`, start the API, then change that password. `POST /identity/register` does not assign roles. If no Admin exists and those two values are empty, the API logs a warning and continues.

## Optional static files

Compose runs Nuxt as Node SSR so the confirm-email helper exists. To serve a generated admin instead, build with `nuxt generate`, mount `.output/public` into Caddy, and replace the default `handle` with `file_server`:

```text
handle {
	root * /srv/admin
	try_files {path} {path}/index.html /index.html
	file_server
}
```

Drop the `admin` service and the `/api/confirm-email` exception when you do that. Email links that open `/identity/confirmEmail` still redirect the browser to `/confirm-email` on the public host. The check-email paste flow calls the Nitro route and needs the Node server.

## Stamped projects

The compose file builds the templates in this repo. After `dotnet new tom-webapi` and `nuxi init`, point `build.context` at those projects. `dotnet new` replaces the `Tom.WebApi` source name inside the API Dockerfile, including the entry assembly name.
