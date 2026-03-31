-- ============================================================
-- NexusAuth Database Schema (PostgreSQL)
--
-- Usage:
--   psql -U postgres -f demo/schema.sql
--
-- This script will:
--   1. Create the 'nexusauth' database (if not exists)
--   2. Connect to it
--   3. Create the 'nexusauth' schema (if not exists)
--   4. Create all 6 tables + indexes under 'nexusauth' schema
--
-- Idempotent: safe to run multiple times.
-- ============================================================

-- Step 1: Create database if not exists
SELECT 'CREATE DATABASE nexusauth'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'nexusauth')

-- Step 2: Connect to the database
\connect nexusauth

-- Step 3: Create schema
CREATE SCHEMA IF NOT EXISTS nexusauth;

-- ============================================================
-- Tables (all under nexusauth schema)
-- ============================================================

-- 1. users
CREATE TABLE IF NOT EXISTS nexusauth.users (
    id              uuid            NOT NULL,
    username        varchar(100)    NOT NULL,
    password_hash   varchar(256)    NOT NULL,
    email           varchar(256),
    phone_number    varchar(20),
    nickname        varchar(100)    NOT NULL,
    gender          smallint        NOT NULL DEFAULT 0,
    ethnicity       varchar(50),
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL,
    updated_at      timestamptz     NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username
    ON nexusauth.users (username);

CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email
    ON nexusauth.users (email) WHERE email IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ix_users_phone_number
    ON nexusauth.users (phone_number) WHERE phone_number IS NOT NULL;

-- 2. oauth_clients
CREATE TABLE IF NOT EXISTS nexusauth.oauth_clients (
    id                  uuid            NOT NULL,
    client_id           varchar(128)    NOT NULL,
    client_secret_hash  varchar(256)    NOT NULL,
    client_name         varchar(256)    NOT NULL,
    description         text,
    redirect_uris       jsonb           NOT NULL,
    allowed_scopes      jsonb           NOT NULL,
    allowed_grant_types jsonb           NOT NULL,
    require_pkce        boolean         NOT NULL DEFAULT true,
    is_active           boolean         NOT NULL DEFAULT true,
    created_at          timestamptz     NOT NULL,
    CONSTRAINT pk_oauth_clients PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_oauth_clients_client_id
    ON nexusauth.oauth_clients (client_id);

-- 3. api_resources
CREATE TABLE IF NOT EXISTS nexusauth.api_resources (
    id              uuid            NOT NULL,
    name            varchar(128)    NOT NULL,
    display_name    varchar(256)    NOT NULL,
    description     text,
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL,
    CONSTRAINT pk_api_resources PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_api_resources_name
    ON nexusauth.api_resources (name);

-- 4. client_api_resources (join table, composite PK, no surrogate id)
CREATE TABLE IF NOT EXISTS nexusauth.client_api_resources (
    client_id       uuid    NOT NULL,
    api_resource_id uuid    NOT NULL,
    CONSTRAINT pk_client_api_resources PRIMARY KEY (client_id, api_resource_id)
);

-- 5. authorization_codes
CREATE TABLE IF NOT EXISTS nexusauth.authorization_codes (
    id                      uuid            NOT NULL,
    code                    varchar(256)    NOT NULL,
    client_id               varchar(128)    NOT NULL,
    user_id                 uuid            NOT NULL,
    redirect_uri            varchar(2048)   NOT NULL,
    scope                   varchar(512)    NOT NULL,
    code_challenge          varchar(256),
    code_challenge_method   varchar(10),
    is_used                 boolean         NOT NULL DEFAULT false,
    expires_at              timestamptz     NOT NULL,
    created_at              timestamptz     NOT NULL,
    CONSTRAINT pk_authorization_codes PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_authorization_codes_code
    ON nexusauth.authorization_codes (code);

-- 6. refresh_tokens
CREATE TABLE IF NOT EXISTS nexusauth.refresh_tokens (
    id          uuid            NOT NULL,
    token       varchar(512)    NOT NULL,
    client_id   varchar(128)    NOT NULL,
    user_id     uuid            NOT NULL,
    scope       varchar(512)    NOT NULL,
    is_revoked  boolean         NOT NULL DEFAULT false,
    expires_at  timestamptz     NOT NULL,
    created_at  timestamptz     NOT NULL,
    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_refresh_tokens_token
    ON nexusauth.refresh_tokens (token);

-- ============================================================
-- Done. Now run seed.sql to insert demo data:
--   psql -U postgres -d nexusauth -f demo/seed.sql
-- ============================================================
