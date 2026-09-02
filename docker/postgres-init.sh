#!/usr/bin/env bash
set -Eeuo pipefail

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

# Register fixed local-only SCIM integration-test credentials. The Workbench
# OAuth resource and client are provisioned by the Workbench API at startup.
psql "${psql_args[@]}" \
  --dbname=nexusauth <<'SQL'
SET search_path TO nexusauth;
INSERT INTO scim_service_principal_credentials
    (id, name, token_hash, scopes, is_active, expires_at, last_used_at, created_at, revoked_at)
VALUES
    (
        '36d7cbb2-02a1-44f7-ad6c-9f68e77e63de',
        'local-scim-integration-read',
        'UJ7alomHnmklWLdv_dk8nAMaTC3HdDPXYlaRzvt4Dvk',
        '["scim:read"]'::jsonb,
        true,
        NULL,
        NULL,
        NOW(),
        NULL
    ),
    (
        'eb06cb73-586f-48c3-b542-6a54549cf391',
        'local-scim-integration-read-write',
        'J0P-nM9qLuo6idK3Rpd_H7cCmbPU6pV5bOB0QQP-FPE',
        '["scim:read","scim:write"]'::jsonb,
        true,
        NULL,
        NULL,
        NOW(),
        NULL
    )
ON CONFLICT (name) DO UPDATE SET
    token_hash = EXCLUDED.token_hash,
    scopes = EXCLUDED.scopes,
    is_active = true,
    expires_at = NULL,
    revoked_at = NULL;
SQL
