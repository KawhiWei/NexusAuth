-- ============================================================
-- NexusAuth demo seed data (single source of truth)
-- 说明：
-- 1) 该脚本是所有 demo 客户端的唯一种子数据来源。
-- 2) 支持的 demo / grant 类型：
--    - Demo.Bff + Demo.Web: authorization_code + refresh_token + OIDC
--    - Demo.ClientCredentials: client_credentials
--    - Demo.DeviceCode: device_code
--    - Demo.RefreshToken: refresh_token（复用 demo-device 客户端）
-- 3) 测试账号密码：alice / Pass@123, bob / Pass@123, admin / Pass@123
-- ============================================================

\connect nexusauth

SET search_path TO nexusauth;

-- ============================================================
-- API resources with audience mapping
-- scope -> audience
-- demo_api, profile_api -> demo-bff-api
-- mobile_api -> demo-mobile-api
-- ============================================================
INSERT INTO api_resources (id, name, display_name, audience, description, is_active, created_at)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'demo_api', 'Demo API', 'demo-bff-api', 'API scope for web BFF business endpoints', true, NOW()),
    ('10000000-0000-0000-0000-000000000002', 'profile_api', 'Profile API', 'demo-bff-api', 'Profile-related API scope for web BFF', true, NOW()),
    ('10000000-0000-0000-0000-000000000003', 'mobile_api', 'Mobile API', 'demo-mobile-api', 'API scope for mobile bearer endpoints', true, NOW())
ON CONFLICT (name) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    audience = EXCLUDED.audience,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;

-- ============================================================
-- OAuth demo clients
-- ============================================================

-- ------------------------------------------------------------
-- OAuth2 / OIDC Demo 1: authorization_code + refresh_token
-- 对应项目：Demo.Bff + Demo.Web
-- client_secret: demo-bff-secret
-- 说明：authorization_code 已按 OAuth 2.1 风格收敛为强制 S256 PKCE。
-- ------------------------------------------------------------
-- client_secret: demo-bff-secret
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000001',
    'demo-bff',
    '$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy',
    'Demo Frontend BFF Client',
    'A front-end/back-end separated demo client for authorization code + OIDC',
    '["http://localhost:5201/signin-oidc"]',
    '["http://localhost:5200/"]',
    '["openid","profile","email","phone","offline_access","demo_api","profile_api"]',
    '["authorization_code","refresh_token"]',
    true,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secret_hash = EXCLUDED.client_secret_hash,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ------------------------------------------------------------
-- OAuth2 Demo 2: client_credentials
-- 对应项目：Demo.ClientCredentials
-- client_secret: demo-bff-secret
-- ------------------------------------------------------------
-- client_secret: demo-bff-secret
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000101',
    'demo-cc',
    '$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy',
    'Demo Client Credentials Client',
    'Console demo client for OAuth2 client_credentials grant',
    '[]',
    '[]',
    '["demo_api"]',
    '["client_credentials"]',
    false,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secret_hash = EXCLUDED.client_secret_hash,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ------------------------------------------------------------
-- OAuth2 Demo 3 + 4: device_code + refresh_token
-- 对应项目：Demo.DeviceCode / Demo.RefreshToken
-- client_secret: demo-bff-secret
-- ------------------------------------------------------------
-- client_secret: demo-bff-secret
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000102',
    'demo-device',
    '$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy',
    'Demo Device Flow Client',
    'Console demo client for OAuth2 device_code and refresh_token grants (public-client friendly)',
    '[]',
    '[]',
    '["openid","profile","email","phone","offline_access","demo_api"]',
    '["urn:ietf:params:oauth:grant-type:device_code","refresh_token"]',
    false,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secret_hash = EXCLUDED.client_secret_hash,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ============================================================
-- Demo users
-- 密码均为：Pass@123
-- ============================================================
INSERT INTO users (id, username, password_hash, email, phone_number, nickname, gender, ethnicity, is_active, created_at, updated_at)
VALUES
(
    '30000000-0000-0000-0000-000000000001',
    'alice',
    '$2a$12$V43kCOSW8gBXQ01do2BW3.a9mIdHOb5Wd1fp5nTMNlBcSpARm0l6S',
    'alice@nexusauth.local',
    '13800000001',
    'Alice Chen',
    2,
    'Han',
    true,
    NOW(),
    NOW()
),
(
    '30000000-0000-0000-0000-000000000002',
    'bob',
    '$2a$12$V43kCOSW8gBXQ01do2BW3.a9mIdHOb5Wd1fp5nTMNlBcSpARm0l6S',
    'bob@nexusauth.local',
    '13800000002',
    'Bob Wang',
    1,
    'Han',
    true,
    NOW(),
    NOW()
),
(
    '30000000-0000-0000-0000-000000000003',
    'admin',
    '$2a$12$V43kCOSW8gBXQ01do2BW3.a9mIdHOb5Wd1fp5nTMNlBcSpARm0l6S',
    'admin@nexusauth.local',
    '13800000003',
    'System Admin',
    1,
    'Han',
    true,
    NOW(),
    NOW()
)
ON CONFLICT (username) DO UPDATE SET
    password_hash = EXCLUDED.password_hash,
    email = EXCLUDED.email,
    phone_number = EXCLUDED.phone_number,
    nickname = EXCLUDED.nickname,
    gender = EXCLUDED.gender,
    ethnicity = EXCLUDED.ethnicity,
    is_active = EXCLUDED.is_active,
    updated_at = NOW();

-- ============================================================
-- Optional mapping table seed (no foreign keys required)
-- ============================================================
INSERT INTO client_api_resources (client_id, api_resource_id)
VALUES
    ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001'),
    ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002'),
    ('20000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000001'),
    ('20000000-0000-0000-0000-000000000102', '10000000-0000-0000-0000-000000000001')
ON CONFLICT (client_id, api_resource_id) DO NOTHING;
