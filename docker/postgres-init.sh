#!/usr/bin/env bash
set -Eeuo pipefail

psql_args=(
  --username "$POSTGRES_USER"
  --no-password
  --set ON_ERROR_STOP=1
)

# production-init.sql drops and recreates nexusauth, so it must be run from
# the maintenance database rather than from the database it creates.
psql "${psql_args[@]}" --dbname=postgres \
  --file=/opt/nexusauth-init/production-init.sql

# Register only the Workbench client required to administer a fresh instance.
# Demo clients and users are intentionally excluded from runtime initialization.
psql "${psql_args[@]}" --dbname=nexusauth <<'SQL'
SET search_path TO nexusauth;
\i /opt/nexusauth-init/admin-seed.sql
SQL
