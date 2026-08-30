-- NexusAuth device-code and login security migration.
--
-- Run against an existing NexusAuth database after
-- database/001_token_secret_hashing.sql. The migration is idempotent.
-- Device-code bearer values are hashed exactly once when migrating the
-- legacy `device_code` column; new databases use `device_code_hash` directly.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
DECLARE
    had_raw_column boolean := false;
BEGIN
    IF to_regclass('nexusauth.device_authorizations') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'device_authorizations'
          AND column_name = 'device_code'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'device_authorizations'
          AND column_name = 'device_code_hash'
    ) THEN
        ALTER TABLE nexusauth.device_authorizations
            RENAME COLUMN device_code TO device_code_hash;
        had_raw_column := true;
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'device_authorizations'
          AND column_name = 'device_code'
    ) THEN
        UPDATE nexusauth.device_authorizations
        SET device_code_hash = device_code;
        had_raw_column := true;
    END IF;

    IF had_raw_column THEN
        UPDATE nexusauth.device_authorizations
        SET device_code_hash = rtrim(
            translate(encode(digest(device_code_hash, 'sha256'), 'base64'), '+/', '-_'),
            '='
        );
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'device_authorizations'
          AND column_name = 'device_code'
    ) THEN
        ALTER TABLE nexusauth.device_authorizations DROP COLUMN device_code;
    END IF;

    ALTER TABLE nexusauth.device_authorizations
        ALTER COLUMN device_code_hash TYPE varchar(43)
        USING device_code_hash::varchar(43),
        ALTER COLUMN device_code_hash SET NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'nexusauth.device_authorizations'::regclass
          AND conname = 'ck_device_authorizations_device_code_hash_base64url'
    ) THEN
        ALTER TABLE nexusauth.device_authorizations
            ADD CONSTRAINT ck_device_authorizations_device_code_hash_base64url
            CHECK (device_code_hash ~ '^[A-Za-z0-9_-]{43}$');
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.ix_device_authorizations_device_code') IS NOT NULL
       AND to_regclass('nexusauth.ix_device_authorizations_device_code_hash') IS NULL THEN
        ALTER INDEX nexusauth.ix_device_authorizations_device_code
            RENAME TO ix_device_authorizations_device_code_hash;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.device_authorizations') IS NOT NULL THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ix_device_authorizations_device_code_hash
            ON nexusauth.device_authorizations (device_code_hash);
        CREATE INDEX IF NOT EXISTS ix_device_authorizations_poll
            ON nexusauth.device_authorizations (device_code_hash, client_id, status, expires_at);
        COMMENT ON COLUMN nexusauth.device_authorizations.device_code_hash IS
            'SHA-256(raw device code), unpadded Base64Url';
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.users') IS NOT NULL THEN
        ALTER TABLE nexusauth.users
            ADD COLUMN IF NOT EXISTS failed_login_attempts integer NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS locked_until timestamptz;

        UPDATE nexusauth.users
        SET failed_login_attempts = 0
        WHERE failed_login_attempts IS NULL;

        ALTER TABLE nexusauth.users
            ALTER COLUMN failed_login_attempts SET DEFAULT 0,
            ALTER COLUMN failed_login_attempts SET NOT NULL;
    END IF;
END
$$;

COMMIT;
