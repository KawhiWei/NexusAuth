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

# The seed files use unqualified table names. Set the application's schema for
# the whole session before including both seed files.
psql "${psql_args[@]}" --dbname=nexusauth <<'SQL'
SET search_path TO nexusauth;
\i /opt/nexusauth-init/demo-seed.sql
\i /opt/nexusauth-init/admin-seed.sql
SQL
