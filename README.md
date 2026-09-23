# Tom

Opinionated ASP.NET Core 10 Web API stamp plus a paired Nuxt 4 admin auth kit. AuthEndpoints is a template dependency, not this repo.

Local development is two origins: the API on `http://localhost:5080`, the admin on `http://localhost:3000`, with the Development CORS policy. Production in [`examples/same-origin`](examples/same-origin) is one public host. Caddy path-splits API routes to the Web API and sends everything else to Nuxt. The browser calls the API on that same host. There is no Nitro, Hono, or Express auth proxy in front of the API.

## Install the API template

From this repository:

```text
dotnet new install ./templates/tom-webapi
```

Then:

```text
dotnet new tom-webapi -n Shop -o ./Shop
```

Uninstall: `dotnet new uninstall Tom.WebApi`.

The stamped API listens on `http://localhost:5080`. See the stamped project README for Postgres, Scalar, and cookie login (`LoginCookie`, not `useCookies`).

`templates/tom-webapi/Dockerfile` is a multi-stage SDK build. The runtime image listens on `8080` and drops to the non-root `app` user. `dotnet new` replaces the `Tom.WebApi` source name in that Dockerfile.

## Install the admin template

```text
npx nuxi@latest init -t gh:madeyoga/tom/templates/tom-admin
```

The stamped Nuxt app listens on `http://localhost:3000`. Set `NUXT_PUBLIC_API_BASE=http://localhost:5080` (see `.env.example`). Pairing steps live in `templates/tom-admin/README.md` and `templates/tom-webapi/README.md`.

`templates/tom-admin/Dockerfile` builds with pnpm and runs `.output/server/index.mjs` with `HOST=0.0.0.0` and `PORT=3000`.

`tom init` is not implemented yet. Use `dotnet new tom-webapi` and `nuxi init` as above.

## Local dual-port

1. Run the API on `:5080` (Development). CORS allows `Frontend:Origin` (`http://localhost:3000`) with credentials.
2. Run the admin on `:3000` with `NUXT_PUBLIC_API_BASE=http://localhost:5080`.
3. Leave `NUXT_API_INTERNAL` unset. The confirm-email Nitro route then calls that public base.

Cookie auth uses `credentials: 'include'`. Do not set `SameSite=None` for this localhost setup.

## Same-origin production

[`examples/same-origin`](examples/same-origin) is a compose stack (Caddy, Web API, Nuxt, Postgres). It is an example in this repo, not files stamped into either template.

Behind Caddy, set the browser base to `.` (`NUXT_PUBLIC_API_BASE`). Blank and `/` mean the same relative base. Nuxt ignores an empty override and would keep `http://localhost:5080`, so the example uses `.`. Set `NUXT_API_INTERNAL=http://webapi:8080` so the confirm-email server fetch does not use that sentinel.

Production leaves CORS off and trusts `X-Forwarded-For` and `X-Forwarded-Proto`. Passkeys `ServerDomain` and the confirm-email redirect / allowed origins are the public host, not the container name. That redirect must be `https`. The example default is `https://localhost` with Caddy's internal CA. DataProtection keys live on a volume. TLS ends at Caddy.

Nuxt stays Node SSR in that compose file. A static `file_server` swap is documented in the example README and is not the default. The check-email paste flow needs the Nitro route, which static files do not run.

Copy `examples/same-origin/.env.example` to `.env` and follow that README. The API template does not ship an EF migration; create one before the stack can seed.
