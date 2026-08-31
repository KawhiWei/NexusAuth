-- ============================================================
-- NexusAuth.Workbench.Api seed data
-- 说明：
-- 1) Workbench API 作为 OAuth 客户端使用 authorization_code + PKCE
-- 2) 认证方式：client_secret_basic
-- 3) 执行前必须通过 psql 变量 workbench_client_secret 提供客户端密钥
-- ============================================================

-- ============================================================
-- API Resources (Scopes)
-- ============================================================
INSERT INTO api_resources (id, name, display_name, audience, description, is_active, created_at)
VALUES
    ('d6898d82-8fdc-4bde-90ba-d43308529093', 'openid', 'OpenID', 'openid', 'Standard OpenID Connect scope', true, NOW()),
    ('1b2d617c-cb23-4db6-8038-e27792f6df40', 'profile', 'Profile', 'profile', 'User profile information', true, NOW()),
    ('4a3adf62-f0c3-42eb-b247-26c572994c87', 'workbench', 'Workbench API', 'workbench', 'NexusAuth Workbench API scope', true, NOW())
ON CONFLICT (name) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    audience = EXCLUDED.audience,
    description = EXCLUDED.description;

-- ============================================================
-- OAuth Client: nexusauth.workbench
-- ============================================================
INSERT INTO oauth_clients (id, client_id, token_endpoint_auth_method, jwks, jwks_uri, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    'a9846c33-0147-44a8-b0be-fc2ddfccd732',
    'nexusauth.workbench',
    'client_secret_basic',
    NULL,
    NULL,
    'NexusAuth Workbench',
    'NexusAuth Workbench Dashboard and API (client_secret_basic)',
    '["http://localhost:5051/signin-oidc"]',
    '["http://localhost:5273/"]',
    '["openid","profile","workbench","offline_access"]',
    '["authorization_code","refresh_token"]',
    true,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    token_endpoint_auth_method = EXCLUDED.token_endpoint_auth_method,
    jwks = EXCLUDED.jwks,
    jwks_uri = EXCLUDED.jwks_uri,
    redirect_uris = EXCLUDED.redirect_uris,
    post_logout_redirect_uris = EXCLUDED.post_logout_redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types,
    require_pkce = EXCLUDED.require_pkce,
    is_active = EXCLUDED.is_active;

-- The Workbench API creates and rotates its BCrypt-protected client secret
-- during startup. Keeping it out of SQL avoids database-specific hashing
-- functions and makes this schema seed deterministic.

-- ============================================================
-- Client API Resource mapping
-- ============================================================
INSERT INTO client_api_resources (client_id, api_resource_id)
VALUES
    ('a9846c33-0147-44a8-b0be-fc2ddfccd732', 'd6898d82-8fdc-4bde-90ba-d43308529093'),
    ('a9846c33-0147-44a8-b0be-fc2ddfccd732', '1b2d617c-cb23-4db6-8038-e27792f6df40'),
    ('a9846c33-0147-44a8-b0be-fc2ddfccd732', '4a3adf62-f0c3-42eb-b247-26c572994c87')
ON CONFLICT (client_id, api_resource_id) DO NOTHING;
