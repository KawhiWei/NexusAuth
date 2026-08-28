-- ============================================================
-- NexusAuth production schema initialization
-- 说明：
-- 1) 本脚本会删除并重建 nexusauth 数据库。
-- 2) 本脚本使用 psql 的 \connect 命令，只适合通过 psql 执行。
-- 3) 本脚本只创建 schema、表、索引，不写入 demo 或 Workbench 种子数据。
-- 4) 本脚本不创建表间外键依赖，保持与当前项目映射一致。
-- 5) 推荐执行：psql -f production-init.sql
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
    external_id     varchar(256),
    password_hash   varchar(256)    NOT NULL,
    email           varchar(256),
    phone_number    varchar(20),
    nickname        varchar(100)    NOT NULL,
    given_name      varchar(100),
    family_name     varchar(100),
    middle_name     varchar(100),
    honorific_prefix varchar(50),
    honorific_suffix varchar(50),
    profile_url     varchar(2048),
    title           varchar(256),
    user_type       varchar(100),
    preferred_language varchar(35),
    locale          varchar(35),
    timezone        varchar(100),
    gender          smallint        NOT NULL DEFAULT 0,
    ethnicity       varchar(50),
    is_active       boolean         NOT NULL DEFAULT true,
    is_system_account boolean       NOT NULL DEFAULT false,
    failed_login_attempts integer   NOT NULL DEFAULT 0,
    locked_until    timestamptz,
    created_at      timestamptz     NOT NULL,
    updated_at      timestamptz     NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_users_username ON nexusauth.users (username);
CREATE UNIQUE INDEX ix_users_external_id ON nexusauth.users (external_id) WHERE external_id IS NOT NULL;
CREATE UNIQUE INDEX ix_users_email ON nexusauth.users (email) WHERE email IS NOT NULL;
CREATE UNIQUE INDEX ix_users_phone_number ON nexusauth.users (phone_number) WHERE phone_number IS NOT NULL;

-- ============================================================
-- oauth_clients
-- ============================================================
CREATE TABLE nexusauth.oauth_clients (
    id                          uuid            NOT NULL,
    client_id                   varchar(128)    NOT NULL,
    token_endpoint_auth_method  varchar(64)     NOT NULL DEFAULT 'client_secret_basic',
    jwks                        text,
    jwks_uri                    varchar(2048),
    client_name                 varchar(256)    NOT NULL,
    description                 text,
    redirect_uris               jsonb           NOT NULL,
    post_logout_redirect_uris   jsonb           NOT NULL,
    allowed_scopes              jsonb           NOT NULL,
    allowed_grant_types         jsonb           NOT NULL,
    require_pkce                boolean         NOT NULL DEFAULT true,
    is_active                   boolean         NOT NULL DEFAULT true,
    created_at                  timestamptz     NOT NULL,
    CONSTRAINT pk_oauth_clients PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_oauth_clients_client_id ON nexusauth.oauth_clients (client_id);

-- ============================================================
-- oauth_client_secrets
-- 说明：
-- 1) shared_secret 的 value 为 BCrypt 哈希。
-- 2) plain_value 仅用于需要原始密钥材料的 client_secret_jwt 场景。
-- ============================================================
CREATE TABLE nexusauth.oauth_client_secrets (
    id              uuid            NOT NULL,
    client_id       uuid            NOT NULL,
    type            varchar(32)     NOT NULL,
    value           text            NOT NULL,
    plain_value     text,
    description     text,
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL,
    CONSTRAINT pk_oauth_client_secrets PRIMARY KEY (id)
);

CREATE INDEX ix_oauth_client_secrets_client_id ON nexusauth.oauth_client_secrets (client_id);
CREATE INDEX ix_oauth_client_secrets_client_id_type ON nexusauth.oauth_client_secrets (client_id, type);

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
    -- SHA-256(raw code), encoded as unpadded Base64Url (43 characters).
    code_hash               varchar(43)     NOT NULL,
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

CREATE UNIQUE INDEX ix_authorization_codes_code_hash ON nexusauth.authorization_codes (code_hash);
CREATE INDEX ix_authorization_codes_consume
    ON nexusauth.authorization_codes (code_hash, client_id, is_used, expires_at);
CREATE INDEX ix_authorization_codes_client_id ON nexusauth.authorization_codes (client_id);
CREATE INDEX ix_authorization_codes_user_id ON nexusauth.authorization_codes (user_id);
ALTER TABLE nexusauth.authorization_codes
    ADD CONSTRAINT ck_authorization_codes_code_hash_base64url
    CHECK (code_hash ~ '^[A-Za-z0-9_-]{43}$');

-- ============================================================
-- refresh_tokens
-- ============================================================
CREATE TABLE nexusauth.refresh_tokens (
    id          uuid            NOT NULL,
    -- SHA-256(raw token), encoded as unpadded Base64Url (43 characters).
    token_hash  varchar(43)     NOT NULL,
    client_id   varchar(128)    NOT NULL,
    user_id     uuid            NOT NULL,
    scope       varchar(512)    NOT NULL,
    is_revoked  boolean         NOT NULL DEFAULT false,
    expires_at  timestamptz     NOT NULL,
    created_at  timestamptz     NOT NULL,
    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_refresh_tokens_token_hash ON nexusauth.refresh_tokens (token_hash);
CREATE INDEX ix_refresh_tokens_rotate
    ON nexusauth.refresh_tokens (token_hash, client_id, is_revoked, expires_at);
CREATE INDEX ix_refresh_tokens_client_id ON nexusauth.refresh_tokens (client_id);
CREATE INDEX ix_refresh_tokens_user_id ON nexusauth.refresh_tokens (user_id);
ALTER TABLE nexusauth.refresh_tokens
    ADD CONSTRAINT ck_refresh_tokens_token_hash_base64url
    CHECK (token_hash ~ '^[A-Za-z0-9_-]{43}$');

-- ============================================================
-- device_authorizations
-- ============================================================
CREATE TABLE nexusauth.device_authorizations (
    id                          uuid            NOT NULL,
    -- SHA-256(raw device code), encoded as unpadded Base64Url (43 characters).
    device_code_hash            varchar(43)     NOT NULL,
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

CREATE UNIQUE INDEX ix_device_authorizations_device_code_hash ON nexusauth.device_authorizations (device_code_hash);
CREATE UNIQUE INDEX ix_device_authorizations_user_code_normalized ON nexusauth.device_authorizations (user_code_normalized);
CREATE INDEX ix_device_authorizations_client_id ON nexusauth.device_authorizations (client_id);
CREATE INDEX ix_device_authorizations_user_id ON nexusauth.device_authorizations (user_id);
CREATE INDEX ix_device_authorizations_poll
    ON nexusauth.device_authorizations (device_code_hash, client_id, status, expires_at);
ALTER TABLE nexusauth.device_authorizations
    ADD CONSTRAINT ck_device_authorizations_device_code_hash_base64url
    CHECK (device_code_hash ~ '^[A-Za-z0-9_-]{43}$');

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

-- ============================================================
-- scim_service_principal_credentials
-- ============================================================
CREATE TABLE nexusauth.scim_service_principal_credentials (
    id          uuid            NOT NULL,
    name        varchar(128)    NOT NULL,
    -- SHA-256(raw bearer token), encoded as unpadded Base64Url.
    token_hash  varchar(43)     NOT NULL,
    scopes      jsonb           NOT NULL,
    is_active   boolean         NOT NULL DEFAULT true,
    expires_at  timestamptz,
    last_used_at timestamptz,
    created_at  timestamptz     NOT NULL,
    revoked_at  timestamptz,
    CONSTRAINT pk_scim_service_principal_credentials PRIMARY KEY (id),
    CONSTRAINT ck_scim_service_principal_credentials_token_hash_base64url
        CHECK (token_hash ~ '^[A-Za-z0-9_-]{43}$')
);

CREATE UNIQUE INDEX ix_scim_service_principal_credentials_name
    ON nexusauth.scim_service_principal_credentials (name);
CREATE UNIQUE INDEX ix_scim_service_principal_credentials_token_hash
    ON nexusauth.scim_service_principal_credentials (token_hash);
