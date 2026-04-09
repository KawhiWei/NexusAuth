-- ============================================================
-- NexusAuth production initialization script
-- 说明：
-- 1) 该脚本只负责生产环境初始化，不会 DROP DATABASE。
-- 2) 该脚本延续当前仓库的表设计，不创建表间外键。
-- 3) 该脚本不写入任何 demo 数据或私钥。
-- 4) 如需初始化生产 client / resource，请使用文末模板并替换占位值。
-- ============================================================

CREATE SCHEMA IF NOT EXISTS nexusauth;
SET search_path TO nexusauth;

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

CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON nexusauth.users (username);
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email ON nexusauth.users (email) WHERE email IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_phone_number ON nexusauth.users (phone_number) WHERE phone_number IS NOT NULL;

CREATE TABLE IF NOT EXISTS nexusauth.oauth_clients (
    id                          uuid            NOT NULL,
    client_id                   varchar(128)    NOT NULL,
    client_secrets              jsonb           NOT NULL DEFAULT '[]'::jsonb,
    token_endpoint_auth_method  varchar(64)     NOT NULL DEFAULT 'client_secret_basic',
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

CREATE UNIQUE INDEX IF NOT EXISTS ix_oauth_clients_client_id ON nexusauth.oauth_clients (client_id);

CREATE TABLE IF NOT EXISTS nexusauth.api_resources (
    id              uuid            NOT NULL,
    name            varchar(128)    NOT NULL,
    display_name    varchar(256)    NOT NULL,
    audience        varchar(256)    NOT NULL,
    description     text,
    is_active       boolean         NOT NULL DEFAULT true,
    created_at      timestamptz     NOT NULL,
    CONSTRAINT pk_api_resources PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_api_resources_name ON nexusauth.api_resources (name);

CREATE TABLE IF NOT EXISTS nexusauth.client_api_resources (
    client_id       uuid    NOT NULL,
    api_resource_id uuid    NOT NULL,
    CONSTRAINT pk_client_api_resources PRIMARY KEY (client_id, api_resource_id)
);

CREATE INDEX IF NOT EXISTS ix_client_api_resources_client_id ON nexusauth.client_api_resources (client_id);
CREATE INDEX IF NOT EXISTS ix_client_api_resources_api_resource_id ON nexusauth.client_api_resources (api_resource_id);

CREATE TABLE IF NOT EXISTS nexusauth.authorization_codes (
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

CREATE UNIQUE INDEX IF NOT EXISTS ix_authorization_codes_code ON nexusauth.authorization_codes (code);
CREATE INDEX IF NOT EXISTS ix_authorization_codes_client_id ON nexusauth.authorization_codes (client_id);
CREATE INDEX IF NOT EXISTS ix_authorization_codes_user_id ON nexusauth.authorization_codes (user_id);

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

CREATE UNIQUE INDEX IF NOT EXISTS ix_refresh_tokens_token ON nexusauth.refresh_tokens (token);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_client_id ON nexusauth.refresh_tokens (client_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id ON nexusauth.refresh_tokens (user_id);

CREATE TABLE IF NOT EXISTS nexusauth.device_authorizations (
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

CREATE UNIQUE INDEX IF NOT EXISTS ix_device_authorizations_device_code ON nexusauth.device_authorizations (device_code);
CREATE UNIQUE INDEX IF NOT EXISTS ix_device_authorizations_user_code_normalized ON nexusauth.device_authorizations (user_code_normalized);
CREATE INDEX IF NOT EXISTS ix_device_authorizations_client_id ON nexusauth.device_authorizations (client_id);
CREATE INDEX IF NOT EXISTS ix_device_authorizations_user_id ON nexusauth.device_authorizations (user_id);

CREATE TABLE IF NOT EXISTS nexusauth.token_blacklist_entries (
    id          uuid            NOT NULL,
    jti         varchar(128)    NOT NULL,
    token_type  varchar(32)     NOT NULL,
    subject     varchar(128),
    expires_at  timestamptz     NOT NULL,
    revoked_at  timestamptz     NOT NULL,
    CONSTRAINT pk_token_blacklist_entries PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_token_blacklist_entries_jti ON nexusauth.token_blacklist_entries (jti);

-- ============================================================
-- Optional bootstrap templates
-- ============================================================

-- 示例：初始化一个 API Resource
-- INSERT INTO nexusauth.api_resources (id, name, display_name, audience, description, is_active, created_at)
-- VALUES (
--     '00000000-0000-0000-0000-000000000101',
--     'your_api',
--     'Your API',
--     'your-api-audience',
--     'Production API resource',
--     true,
--     NOW()
-- )
-- ON CONFLICT (name) DO UPDATE SET
--     display_name = EXCLUDED.display_name,
--     audience = EXCLUDED.audience,
--     description = EXCLUDED.description,
--     is_active = EXCLUDED.is_active;

-- 示例：初始化一个 private_key_jwt 客户端
-- INSERT INTO nexusauth.oauth_clients (
--     id,
--     client_id,
--     client_secrets,
--     token_endpoint_auth_method,
--     client_name,
--     description,
--     redirect_uris,
--     post_logout_redirect_uris,
--     allowed_scopes,
--     allowed_grant_types,
--     require_pkce,
--     is_active,
--     created_at)
-- VALUES (
--     '00000000-0000-0000-0000-000000000201',
--     'your-bff-client',
--     jsonb_build_array(
--         jsonb_build_object(
--             'Type', 'jwks',
--             'Value', '{"keys":[{"kty":"RSA","kid":"your-bff-key-1","use":"sig","alg":"RS256","n":"<replace-n>","e":"AQAB"}]}',
--             'Description', 'primary_jwks'
--         )
--     ),
--     'private_key_jwt',
--     'Your BFF Client',
--     'Production BFF client',
--     '["https://your-bff.example.com/signin-oidc"]',
--     '["https://your-frontend.example.com/"]',
--     '["openid","profile","email","offline_access","your_api"]',
--     '["authorization_code","refresh_token"]',
--     true,
--     true,
--     NOW()
-- )
-- ON CONFLICT (client_id) DO UPDATE SET
--     client_secrets = EXCLUDED.client_secrets,
--     token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
--     redirect_uris = EXCLUDED.redirect_uris,
--     post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
--     allowed_scopes = EXCLUDED.allowed_scopes,
--     allowed_grant_types = EXCLUDED.allowed_grant_types,
--     require_pkce = EXCLUDED.require_pkce,
--     is_active = EXCLUDED.is_active;
