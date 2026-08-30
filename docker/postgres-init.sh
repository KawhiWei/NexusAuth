#!/usr/bin/env bash
set -Eeuo pipefail

: "${WORKBENCH_CLIENT_SECRET:?WORKBENCH_CLIENT_SECRET must be set before initializing the Workbench OAuth client}"

psql_args=(
  --username "$POSTGRES_USER"
  --no-password
  --set ON_ERROR_STOP=1
)

# PostgreSQL executes this hook only for a brand-new data volume. Keep the
# final schema script non-destructive so it is also safe for managed services.
if ! psql "${psql_args[@]}" --dbname=postgres --tuples-only --no-align \
  --command="SELECT 1 FROM pg_database WHERE datname = 'nexusauth'" | grep -qx '1'; then
  psql "${psql_args[@]}" --dbname=postgres --command='CREATE DATABASE nexusauth'
fi

psql "${psql_args[@]}" --dbname=nexusauth \
  --file=/opt/nexusauth-init/production-init.sql

# Register only the Workbench client required to administer a fresh instance.
# Demo clients and users are intentionally excluded from runtime initialization.
psql "${psql_args[@]}" \
  --set "workbench_client_secret=$WORKBENCH_CLIENT_SECRET" \
  --dbname=nexusauth <<'SQL'
SET search_path TO nexusauth;
\i /opt/nexusauth-init/admin-seed.sql
SQL
