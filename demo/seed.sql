-- ============================================================
-- NexusAuth demo seed data (latest)
-- 说明：
-- 1) 该脚本用于初始化当前 Demo 所需的全部测试数据。
-- 2) 账号密码：alice / Pass@123, bob / Pass@123, admin / Pass@123
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
-- OAuth clients
-- ============================================================

-- client_secret: demo-bff-secret
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000001',
    'demo-bff',
    '$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy',
    'Demo Frontend BFF Client',
    'A front-end/back-end separated demo client for authorization code + OIDC',
    '["http://localhost:5201/signin-oidc"]',
    '["openid","profile","email","phone","offline_access","demo_api","profile_api"]',
    '["authorization_code","refresh_token"]',
    true,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secret_hash = EXCLUDED.client_secret_hash,
    redirect_uris = EXCLUDED.redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- client_secret: demo-mobile-secret
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000002',
    'demo-mobile',
    '$2a$12$9TGL4MAZQqJ5T9wl.e/m4O0z8QsVRTVo9J0WVBC.Pa6iveKKGVceG',
    'Demo Mobile Client',
    'A demo mobile app client for authorization code + PKCE',
    '["myapp://auth/callback"]',
    '["openid","profile","email","phone","offline_access","mobile_api"]',
    '["authorization_code","refresh_token"]',
    true,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secret_hash = EXCLUDED.client_secret_hash,
    redirect_uris = EXCLUDED.redirect_uris,
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
    ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000003')
ON CONFLICT (client_id, api_resource_id) DO NOTHING;
