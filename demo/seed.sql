-- ============================================================
-- NexusAuth demo seed data (single source of truth)
-- 说明：
-- 1) 该脚本是所有 demo 客户端的唯一种子数据来源。
-- 2) Demo.Bff / Demo.Web 保持为 private_key_jwt。
-- 3) 另外提供一套 Demo.Bff.ClientSecret / Demo.Web.ClientSecret 用于 client_secret_basic 演示。
-- 3) oauth_clients.client_secrets 使用统一多类型结构，预留后续扩展。
-- 4) 测试账号密码：alice / Pass@123, bob / Pass@123, admin / Pass@123
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
-- auth_method: private_key_jwt
-- 说明：authorization_code 已按 OAuth 2.1 风格收敛为强制 S256 PKCE。
-- ------------------------------------------------------------
-- key_id: demo-bff-key-1
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000001',
    'demo-bff',
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'jwks',
            'Value', '{"keys":[{"kty":"RSA","kid":"demo-bff-key-1","use":"sig","alg":"RS256","n":"7Bghnu8yZRRAnvax9eqUO0MziC3hSa4XKBP-mEE8WpFs8smd9PLgulFHCJEgZdTpT7eM6i-1HrQl66F2yP9iZiZ8cKbpYzL-QKH8ii8VLaXJ9bMR7mEKPAOU85H3tQS0W5RMGemXdN78o7oQwn8p3_mAoKby77YI3EcwNBaoZo51ud7x7jAxBGA0IiHhTqCrJEo4t3eXREIepEN5xBXAcnTTqTdrUQXWrEL0bD06Hud_xm2SapuxbogLkPKK3keRhTPSbEcZrAbVsNr08WnCMoGa6QsSSpj6Bk4kFF-nJDZyENU0IzZO28n_Uz0bSgEHGlIl0Qe2iv9hz6wdlHw6-Q","e":"AQAB"}]}',
            'Description', 'demo-bff-jwks'
        )
    ),
    'private_key_jwt',
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
    client_secrets = EXCLUDED.client_secrets,
    token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ------------------------------------------------------------
-- OAuth2 Demo 1B: authorization_code + refresh_token + OIDC (client_secret_basic)
-- 对应项目：Demo.Bff.ClientSecret + Demo.Web.ClientSecret
-- client_secret: demo-bff-secret
-- ------------------------------------------------------------
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000002',
    'demo-bff-secret',
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'shared_secret',
            'Value', '$2a$12$pw856E1CHH3FfcshE0NwCeETGR5hyYaeudBqZfQYCpXdbBuvOpuuy',
            'Description', 'demo-bff-secret'
        )
    ),
    'client_secret_basic',
    'Demo Frontend BFF Client Secret',
    'A front-end/back-end separated demo client for authorization code + OIDC using client_secret_basic',
    '["http://localhost:5301/signin-oidc"]',
    '["http://localhost:5300/"]',
    '["openid","profile","email","phone","offline_access","demo_api","profile_api"]',
    '["authorization_code","refresh_token"]',
    true,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secrets = EXCLUDED.client_secrets,
    token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ------------------------------------------------------------
-- OAuth2 Demo 2: client_credentials
-- 对应项目：Demo.ClientCredentials
-- auth_method: private_key_jwt
-- ------------------------------------------------------------
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000101',
    'demo-cc',
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'jwks',
            'Value', '{"keys":[{"kty":"RSA","kid":"demo-cc-key-1","use":"sig","alg":"RS256","n":"qcGBVp2tCI3PlfndpcWi5MPT-6sNej-_El3p5ivGDzUy_26Do6R2Bak3o4uJ7a4im5WuHtBRmfzgSJlPl7GPuc5KEjnhIupBtvbpKnEhSFKpcYt-0-E6lIA-asudW2AUgXGWjGO53e9_kml-qZ63E976op-tkecILxDpha846YbHIvpE4_hZLxmw4jCpGSzjvJdnM80po5qguwktBHCQuvUVSk2j_5RwiiN9bKHYNiWoIzpwXNXGyvbaANuk_FYIcHLG-kPHUMuWZSpt7lAq1Z045aiG_Nat9aBAA76klOcyuOF6-FI3qQenkoAEHC-VgG3NsNPRn25CJes0varcdQ","e":"AQAB"}]}',
            'Description', 'demo-cc-jwks'
        )
    ),
    'private_key_jwt',
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
    client_secrets = EXCLUDED.client_secrets,
    token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ------------------------------------------------------------
-- OAuth2 Demo 3 + 4: device_code + refresh_token
-- 对应项目：Demo.DeviceCode / Demo.RefreshToken
-- auth_method: private_key_jwt
-- ------------------------------------------------------------
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000102',
    'demo-device',
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'jwks',
            'Value', '{"keys":[{"kty":"RSA","kid":"demo-device-key-1","use":"sig","alg":"RS256","n":"yxfXmgXAnVynvxs0fGwdlmjA71O6tS8O3UuGDZ_7Z0BQ4t_T3fCjIRJeqQ3_-sHG-cocuKm6eCmbinEoneoQVbKZ7HjL_HmWQ2vm2CfpwvtY719R43oHLn1vXxx87kKqDVpCJwETlC-iyA9OkNlbSASY5aFTc5wFd1uxjEUUUwJm9yMn6lclZn23Mt32ayh7j6cfuCWSSI2jeV6v0wikeDm3MYE9G42PZJ9SX-fSsCgY7u3u9OopLgwEUBhnpQT1MEWsxy_hVqNpD-6nVOhhJdw5lka1qEzZJ1Keoq5xib4SmGDGOrEzebd-qq9XMWKNfvZ-BEzCcp1vyHUJ0B7HiQ","e":"AQAB"}]}',
            'Description', 'demo-device-jwks'
        )
    ),
    'private_key_jwt',
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
    client_secrets = EXCLUDED.client_secrets,
    token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- ------------------------------------------------------------
-- OAuth2 Demo 5: private_key_jwt (JWKS based)
-- 对应项目：保留为独立 machine client，用于验证 private_key_jwt 客户端认证
-- ------------------------------------------------------------
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000103',
    'demo-cert',
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'jwks',
            'Value', '{"keys":[{"kty":"RSA","kid":"demo-cert-key-1","use":"sig","alg":"RS256","n":"pPbIZWa9Kor7p4jaPrNrv29XigyKHTuBU3QG7xDRgy5GqbX16QBl715QtgCqe6mQAYhrazLH7LIecQ1ymeD7OCy6Bm0llgvGSTr8X_JU8dsrVN6MF7ab31bcOy1GggTdXpU-5QTfVcyd0MxQw1zxRubEj4jEhw2abUEn7PAdWgVLyB4r-sz0uMq7DQkUhOI8horrKowP_3rqumuba54Pj4hRwmKoSzEpW3MGvwhaanDzqTSWng00ooOysTNZou198Xpwq-BXxeX868KS-ud4DbebbzOQ85eyToRKC80jJscvlD8QcJJ6Uh_rcJPGJfaZPKgtwLy4q1szxFhXqUepXw","e":"AQAB"}]}',
            'Description', 'demo-cert-jwks'
        )
    ),
    'private_key_jwt',
    'Demo Certificate Client',
    'Console demo client for OAuth2 private_key_jwt token endpoint auth',
    '[]',
    '[]',
    '["demo_api"]',
    '["client_credentials"]',
    false,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secrets = EXCLUDED.client_secrets,
    token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
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
    ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001'),
    ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002'),
    ('20000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000001'),
    ('20000000-0000-0000-0000-000000000102', '10000000-0000-0000-0000-000000000001'),
    ('20000000-0000-0000-0000-000000000103', '10000000-0000-0000-0000-000000000001')
ON CONFLICT (client_id, api_resource_id) DO NOTHING;
