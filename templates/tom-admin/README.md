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

## Pair with tom-webapi

1. Stamp and run the API on `:5080`:

   ```text
   dotnet new install ./templates/tom-webapi
   dotnet new tom-webapi -n Shop -o ./Shop
   cd Shop
   cp .env.example .env   # set Seed__Password
   dotnet tool restore
   dotnet run
   ```

2. Run this admin on `:3000` with `NUXT_PUBLIC_API_BASE=http://localhost:5080`.
3. Development CORS on the API allows `Frontend:Origin` (`http://localhost:3000`) with credentials. Production should reverse-proxy the SPA onto the same origin and leave CORS off.
4. Auth calls go to `/identity/*` (CSRF header `RequestVerificationToken`). Passkeys use `/account/passkeys/*`.
5. Confirm-email links redirect to `http://localhost:3000/confirm-email`. Identity mail is written to `emails/` on the API host.

## Scripts

```text
pnpm install
pnpm dev
pnpm typecheck
```
