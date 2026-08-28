-- SCIM 2.0 service-principal bearer credentials.
-- Only SHA-256 Base64Url hashes are persisted; raw tokens are returned once
-- by the credential-management operation and are never stored in this table.
-- This migration is safe to run repeatedly against an existing database.

BEGIN;

CREATE TABLE IF NOT EXISTS nexusauth.scim_service_principal_credentials (
    id           uuid            NOT NULL,
    name         varchar(128)    NOT NULL,
    token_hash   varchar(43)     NOT NULL,
    scopes       jsonb           NOT NULL DEFAULT '[]'::jsonb,
    is_active    boolean         NOT NULL DEFAULT true,
    expires_at   timestamptz,
    last_used_at timestamptz,
    created_at   timestamptz     NOT NULL,
    revoked_at   timestamptz,
    CONSTRAINT pk_scim_service_principal_credentials PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_scim_service_principal_credentials_name
    ON nexusauth.scim_service_principal_credentials (name);
CREATE UNIQUE INDEX IF NOT EXISTS ix_scim_service_principal_credentials_token_hash
    ON nexusauth.scim_service_principal_credentials (token_hash);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'nexusauth.scim_service_principal_credentials'::regclass
          AND conname = 'ck_scim_service_principal_credentials_token_hash_base64url'
    ) THEN
        ALTER TABLE nexusauth.scim_service_principal_credentials
            ADD CONSTRAINT ck_scim_service_principal_credentials_token_hash_base64url
            CHECK (token_hash ~ '^[A-Za-z0-9_-]{43}$');
    END IF;
END
$$;

COMMENT ON COLUMN nexusauth.scim_service_principal_credentials.token_hash IS
    'SHA-256(raw SCIM bearer token), unpadded Base64Url';

COMMIT;
