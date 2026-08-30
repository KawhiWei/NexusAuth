-- NexusAuth token secret hardening migration.
--
-- Run this file while connected to the existing NexusAuth database, for
-- example: psql --dbname=nexusauth --file=database/001_token_secret_hashing.sql
--
-- The legacy `code` and `token` columns contain raw bearer values. On the
-- first run they are renamed to *_hash and every value is hashed exactly once.
-- Once the legacy column has been removed, subsequent runs only enforce the
-- hash column type, constraints, and indexes; already-migrated values are not
-- hashed a second time.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
DECLARE
    had_raw_column boolean := false;
BEGIN
    IF to_regclass('nexusauth.authorization_codes') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'authorization_codes'
          AND column_name = 'code'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'authorization_codes'
          AND column_name = 'code_hash'
    ) THEN
        ALTER TABLE nexusauth.authorization_codes RENAME COLUMN code TO code_hash;
        had_raw_column := true;
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'authorization_codes'
          AND column_name = 'code'
    ) THEN
        -- A partially prepared schema may have both columns. The legacy
        -- column is authoritative because it is the known raw value.
        UPDATE nexusauth.authorization_codes
        SET code_hash = code;
        had_raw_column := true;
    END IF;

    IF had_raw_column THEN
        UPDATE nexusauth.authorization_codes
        SET code_hash = rtrim(
            translate(encode(digest(code_hash, 'sha256'), 'base64'), '+/', '-_'),
            '='
        );
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'authorization_codes'
          AND column_name = 'code'
    ) THEN
        ALTER TABLE nexusauth.authorization_codes DROP COLUMN code;
    END IF;

    ALTER TABLE nexusauth.authorization_codes
        ALTER COLUMN code_hash TYPE varchar(43)
        USING code_hash::varchar(43),
        ALTER COLUMN code_hash SET NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'nexusauth.authorization_codes'::regclass
          AND conname = 'ck_authorization_codes_code_hash_base64url'
    ) THEN
        ALTER TABLE nexusauth.authorization_codes
            ADD CONSTRAINT ck_authorization_codes_code_hash_base64url
            CHECK (code_hash ~ '^[A-Za-z0-9_-]{43}$');
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.ix_authorization_codes_code') IS NOT NULL
       AND to_regclass('nexusauth.ix_authorization_codes_code_hash') IS NULL THEN
        ALTER INDEX nexusauth.ix_authorization_codes_code
            RENAME TO ix_authorization_codes_code_hash;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.authorization_codes') IS NOT NULL THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ix_authorization_codes_code_hash
            ON nexusauth.authorization_codes (code_hash);
        CREATE INDEX IF NOT EXISTS ix_authorization_codes_consume
            ON nexusauth.authorization_codes (code_hash, client_id, is_used, expires_at);
        COMMENT ON COLUMN nexusauth.authorization_codes.code_hash IS
            'SHA-256(raw authorization code), unpadded Base64Url';
    END IF;
END
$$;

DO $$
DECLARE
    had_raw_column boolean := false;
BEGIN
    IF to_regclass('nexusauth.refresh_tokens') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'refresh_tokens'
          AND column_name = 'token'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'refresh_tokens'
          AND column_name = 'token_hash'
    ) THEN
        ALTER TABLE nexusauth.refresh_tokens RENAME COLUMN token TO token_hash;
        had_raw_column := true;
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'refresh_tokens'
          AND column_name = 'token'
    ) THEN
        -- A partially prepared schema may have both columns. The legacy
        -- column is authoritative because it is the known raw value.
        UPDATE nexusauth.refresh_tokens
        SET token_hash = token;
        had_raw_column := true;
    END IF;

    IF had_raw_column THEN
        UPDATE nexusauth.refresh_tokens
        SET token_hash = rtrim(
            translate(encode(digest(token_hash, 'sha256'), 'base64'), '+/', '-_'),
            '='
        );
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'nexusauth'
          AND table_name = 'refresh_tokens'
          AND column_name = 'token'
    ) THEN
        ALTER TABLE nexusauth.refresh_tokens DROP COLUMN token;
    END IF;

    ALTER TABLE nexusauth.refresh_tokens
        ALTER COLUMN token_hash TYPE varchar(43)
        USING token_hash::varchar(43),
        ALTER COLUMN token_hash SET NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'nexusauth.refresh_tokens'::regclass
          AND conname = 'ck_refresh_tokens_token_hash_base64url'
    ) THEN
        ALTER TABLE nexusauth.refresh_tokens
            ADD CONSTRAINT ck_refresh_tokens_token_hash_base64url
            CHECK (token_hash ~ '^[A-Za-z0-9_-]{43}$');
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.ix_refresh_tokens_token') IS NOT NULL
       AND to_regclass('nexusauth.ix_refresh_tokens_token_hash') IS NULL THEN
        ALTER INDEX nexusauth.ix_refresh_tokens_token
            RENAME TO ix_refresh_tokens_token_hash;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('nexusauth.refresh_tokens') IS NOT NULL THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ix_refresh_tokens_token_hash
            ON nexusauth.refresh_tokens (token_hash);
        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_rotate
            ON nexusauth.refresh_tokens (token_hash, client_id, is_revoked, expires_at);
        COMMENT ON COLUMN nexusauth.refresh_tokens.token_hash IS
            'SHA-256(raw refresh token), unpadded Base64Url';
    END IF;
END
$$;

COMMIT;
