-- ============================================================
-- Demo seed data for NexusAuth
-- 说明：本脚本会重建一套前后分离 Demo 所需的客户端、资源和测试用户。
-- 登录账号：alice / Pass@123, bob / Pass@123, admin / Pass@123
-- ============================================================

\connect nexusauth

SET search_path TO nexusauth;

INSERT INTO api_resources (id, name, display_name, description, is_active, created_at)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'demo_api', 'Demo API', 'API scope for the demo BFF backend', true, NOW()),
    ('10000000-0000-0000-0000-000000000002', 'profile_api', 'Profile API', 'Profile scope exposed through OIDC userinfo', true, NOW())
ON CONFLICT (name) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    is_active = EXCLUDED.is_active;

-- client_secret: demo-bff-secret
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000001',
    'demo-bff',
    '$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy',
    'Demo Frontend BFF Client',
    'A front-end/back-end separated demo client for authorization code + OIDC',
    '["http://localhost:5201/signin-oidc"]',
    '["openid","profile","email","phone","offline_access","demo_api"]',
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

-- 说明：如果你需要 client_api_resources 关系，可以按实际业务继续补；当前授权服务主要依赖 allowed_scopes。
