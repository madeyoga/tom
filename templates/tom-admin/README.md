# Tom Admin

Nuxt 4 + Nuxt UI cookie auth kit. Pair with [`tom-webapi`](../tom-webapi) for register, login, email confirmation, 2FA, passkeys, security settings, and step-up.

## Create a project

```text
npx nuxi@latest init -t gh:madeyoga/tom/templates/tom-admin
```

Then:

```text
cd <your-app>
cp .env.example .env
pnpm install
pnpm dev
```

Nuxt listens on `http://localhost:3000`. Set `NUXT_PUBLIC_API_BASE` (default `http://localhost:5080`) so it matches the paired API `applicationUrl`.

`Dockerfile` builds with pnpm and runs `.output/server/index.mjs` as the non-root `node` user, with `HOST=0.0.0.0` and `PORT=3000`.

## Pair with tom-webapi

1. Stamp and run the API on `:5080`:

   ```text
   dotnet new install ./templates/tom-webapi
   dotnet new tom-webapi -n Shop -o ./Shop
   cd Shop
   cp .env.example .env   # set Seed__Password
   dotnet tool restore
   dotnet run --project src/Shop.Api
   ```

2. Run this admin on `:3000` with `NUXT_PUBLIC_API_BASE=http://localhost:5080`. Leave `NUXT_API_INTERNAL` unset.
3. Development CORS on the API allows `Frontend:Origin` (`http://localhost:3000`) with credentials. Production leaves CORS off.
4. Auth calls go to `/identity/*` (CSRF header `RequestVerificationToken`) with `credentials: 'include'`. Passkeys use `/account/passkeys/*`. External logins use `/auth/*`.
5. Confirm-email links redirect to `http://localhost:3000/confirm-email`. Identity mail is written to `emails/` on the API host. The check-email page calls this app's Nitro route `/api/confirm-email`, which fetches `/identity/confirmEmail` on the API. Locally that fetch uses `NUXT_PUBLIC_API_BASE`.

## Same-origin production

[`examples/same-origin`](../../examples/same-origin) puts Caddy, this app, the API, and Postgres on one public host. Caddy sends the API prefixes to the Web API and every other path here. The browser base is relative: set `NUXT_PUBLIC_API_BASE` to `.` (blank and `/` mean the same thing). Nuxt ignores an empty override and would keep `http://localhost:5080`, so the example uses `.`.

Set `NUXT_API_INTERNAL=http://webapi:8080` for the Nitro confirm-email fetch. That route must not call the empty public base from inside the container. `/api/confirm-email` is the only API-looking path Caddy leaves on Nuxt.

`credentials: 'include'` stays. Cookies are host-only. The API's passkeys `ServerDomain` is the public hostname. The confirm-email redirect and allowed origins are that host's `https` origin. Production rejects an `http` redirect.

This image is Node SSR, which is what compose runs. A static `file_server` swap is documented in the example README and is not the default. Static files do not run the Nitro confirm-email helper. Email links that open `/identity/confirmEmail` still redirect the browser to `/confirm-email`.

## Scripts

```text
pnpm install
pnpm dev
pnpm typecheck
```
