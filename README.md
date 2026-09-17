# Tom

Opinionated ASP.NET Core 10 Web API stamp. AuthEndpoints is a template dependency, not this repo.

## Install the template

From this repository:

```text
dotnet new install ./templates/tom-webapi
```

Then:

```text
dotnet new tom-webapi -n Shop -o ./Shop
```

Uninstall: `dotnet new uninstall Tom.WebApi`.

See the stamped project README for Postgres, Scalar, and cookie login (`LoginCookie`, not `useCookies`).

`tom init` is not implemented yet. Use `dotnet new tom-webapi`.
