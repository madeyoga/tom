# Tom

Opinionated ASP.NET Core 10 Web API stamp plus a paired Nuxt 4 admin auth kit. AuthEndpoints is a template dependency, not this repo.

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

## Install the admin template

```text
npx nuxi@latest init -t gh:madeyoga/tom/templates/tom-admin
```

The stamped Nuxt app listens on `http://localhost:3000`. Set `NUXT_PUBLIC_API_BASE=http://localhost:5080` (see `.env.example`). Pairing steps live in `templates/tom-admin/README.md` and `templates/tom-webapi/README.md`.

`tom init` is not implemented yet. Use `dotnet new tom-webapi` and `nuxi init` as above.
