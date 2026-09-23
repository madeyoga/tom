#!/bin/sh
set -eu

mkdir -p /app/keys /app/emails

if [ "$(id -u)" = "0" ]; then
  chown -R app:app /app/keys /app/emails
  exec runuser -u app -- dotnet Tom.WebApi.dll "$@"
fi

exec dotnet Tom.WebApi.dll "$@"
