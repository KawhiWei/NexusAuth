#!/usr/bin/env bash
set -Eeuo pipefail

database_name="${WEBAUTHN_NEXUSAUTH_DATABASE:-WebAuthnNexusAuth}"
psql_args=(--username "$POSTGRES_USER" --no-password --set ON_ERROR_STOP=1)

if [[ ! "$database_name" =~ ^[A-Za-z0-9_]+$ ]]; then
  echo "WEBAUTHN_NEXUSAUTH_DATABASE may contain only letters, numbers, and underscores." >&2
  exit 1
fi

if ! psql "${psql_args[@]}" --dbname=postgres --tuples-only --no-align \
  --command="SELECT 1 FROM pg_database WHERE datname = '$database_name'" | grep -qx '1'; then
  psql "${psql_args[@]}" --dbname=postgres --command="CREATE DATABASE \"$database_name\""
fi

psql "${psql_args[@]}" --dbname="$database_name" \
  --file=/opt/webauthn-nexusauth-init/webauthn-production-init.sql
