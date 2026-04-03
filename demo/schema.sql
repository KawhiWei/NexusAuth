-- ============================================================
-- NexusAuth demo schema (full reset)
-- 说明：
-- 1) 本脚本会删除并重建 nexusauth 数据库。
-- 2) 不创建任何表间外键依赖（按你的要求）。
-- 3) 表结构对齐当前代码中的 DbContext + EF 配置。
-- ============================================================

SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'nexusauth'
  AND pid <> pg_backend_pid();

DROP DATABASE IF EXISTS nexusauth;
CREATE DATABASE nexusauth;

\connect nexusauth

CREATE SCHEMA IF NOT EXISTS nexusauth;
SET search_path TO nexusauth;

-- ============================================================
-- users
-- ============================================================
CREATE TABLE nexusauth.users (
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

CREATE UNIQUE INDEX ix_users_username ON nexusauth.users (username);
CREATE UNIQUE INDEX ix_users_email ON nexusauth.users (email) WHERE email IS NOT NULL;
CREATE UNIQUE INDEX ix_users_phone_number ON nexusauth.users (phone_number) WHERE phone_number IS NOT NULL;

-- ============================================================
-- oauth_clients
-- ============================================================
CREATE TABLE nexusauth.oauth_clients (
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

CREATE UNIQUE INDEX ix_oauth_clients_client_id ON nexusauth.oauth_clients (client_id);

-- ============================================================
-- api_resources
-- ============================================================
CREATE TABLE nexusauth.api_resources (
    id              uuid            NOT NULL,
    name            varchar(128)    NOT NULL,
    display_name    varchar(256)    NOT NULL,
    audience        varchar(256)    NOT NULL,
    description     text,
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL,
    CONSTRAINT pk_api_resources PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_api_resources_name ON nexusauth.api_resources (name);

-- ============================================================
-- client_api_resources
-- 说明：只保留组合主键，不建立外键。
-- ============================================================
CREATE TABLE nexusauth.client_api_resources (
    client_id       uuid    NOT NULL,
    api_resource_id uuid    NOT NULL,
    CONSTRAINT pk_client_api_resources PRIMARY KEY (client_id, api_resource_id)
);

CREATE INDEX ix_client_api_resources_client_id ON nexusauth.client_api_resources (client_id);
CREATE INDEX ix_client_api_resources_api_resource_id ON nexusauth.client_api_resources (api_resource_id);

-- ============================================================
-- authorization_codes
-- ============================================================
CREATE TABLE nexusauth.authorization_codes (
    id                      uuid            NOT NULL,
    code                    varchar(256)    NOT NULL,
    client_id               varchar(128)    NOT NULL,
    user_id                 uuid            NOT NULL,
    redirect_uri            varchar(2048)   NOT NULL,
    scope                   varchar(512)    NOT NULL,
    code_challenge          varchar(256),
    code_challenge_method   varchar(10),
    nonce                   varchar(256),
    claims_json             text,
    authenticated_at        timestamptz,
    acr                     varchar(128),
    amr                     varchar(256),
    is_used                 boolean         NOT NULL DEFAULT false,
    expires_at              timestamptz     NOT NULL,
    created_at              timestamptz     NOT NULL,
    CONSTRAINT pk_authorization_codes PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_authorization_codes_code ON nexusauth.authorization_codes (code);
CREATE INDEX ix_authorization_codes_client_id ON nexusauth.authorization_codes (client_id);
CREATE INDEX ix_authorization_codes_user_id ON nexusauth.authorization_codes (user_id);

-- ============================================================
-- refresh_tokens
-- ============================================================
CREATE TABLE nexusauth.refresh_tokens (
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

CREATE UNIQUE INDEX ix_refresh_tokens_token ON nexusauth.refresh_tokens (token);
CREATE INDEX ix_refresh_tokens_client_id ON nexusauth.refresh_tokens (client_id);
CREATE INDEX ix_refresh_tokens_user_id ON nexusauth.refresh_tokens (user_id);

-- ============================================================
-- device_authorizations
-- ============================================================
CREATE TABLE nexusauth.device_authorizations (
    id                          uuid            NOT NULL,
    device_code                 varchar(256)    NOT NULL,
    user_code                   varchar(32)     NOT NULL,
    user_code_normalized        varchar(32)     NOT NULL,
    client_id                   varchar(128)    NOT NULL,
    scope                       varchar(512)    NOT NULL,
    user_id                     uuid,
    status                      varchar(32)     NOT NULL,
    polling_interval_seconds    integer         NOT NULL,
    expires_at                  timestamptz     NOT NULL,
    created_at                  timestamptz     NOT NULL,
    authorized_at               timestamptz,
    last_polled_at              timestamptz,
    CONSTRAINT pk_device_authorizations PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_device_authorizations_device_code ON nexusauth.device_authorizations (device_code);
CREATE UNIQUE INDEX ix_device_authorizations_user_code_normalized ON nexusauth.device_authorizations (user_code_normalized);
CREATE INDEX ix_device_authorizations_client_id ON nexusauth.device_authorizations (client_id);
CREATE INDEX ix_device_authorizations_user_id ON nexusauth.device_authorizations (user_id);

-- ============================================================
-- token_blacklist_entries
-- ============================================================
CREATE TABLE nexusauth.token_blacklist_entries (
    id          uuid            NOT NULL,
    jti         varchar(128)    NOT NULL,
    token_type  varchar(32)     NOT NULL,
    subject     varchar(128),
    expires_at  timestamptz     NOT NULL,
    revoked_at  timestamptz     NOT NULL,
    CONSTRAINT pk_token_blacklist_entries PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_token_blacklist_entries_jti ON nexusauth.token_blacklist_entries (jti);
