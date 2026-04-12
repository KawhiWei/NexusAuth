-- ============================================================
-- NexusAuth.Workbench.Api seed data
-- 说明：
-- 1) Workbench API 作为 OAuth 客户端使用 authorization_code + PKCE
-- 2) 认证方式：client_secret_basic
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
-- OAuth Client: NexusAuth.Workbench (参考 demo-bff-secret 格式)
-- ============================================================
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    'a9846c33-0147-44a8-b0be-fc2ddfccd732',
    'NexusAuth.Workbench',
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'shared_secret',
            'Value', '$2a$12$vOC8fSO5ti5eIJ1Hl5in.OjL3ZxP99yUf5nVHWBwN26aaa9/P7nm2',
            'Description', 'Client secret (c57cf0e110c54ad2ac5591b99801b852)'
        )
    ),
    'client_secret_basic',
    'NexusAuth Workbench',
    'NexusAuth Workbench Dashboard and API (client_secret_basic)',
    '["http://localhost:5051/signin-oidc"]',
    '["http://localhost:5273/"]',
    '["openid","profile","workbench"]',
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

-- ============================================================
-- Client API Resource mapping
-- ============================================================
INSERT INTO client_api_resources (client_id, api_resource_id)
VALUES
    ('a9846c33-0147-44a8-b0be-fc2ddfccd732', 'd6898d82-8fdc-4bde-90ba-d43308529093'),
    ('a9846c33-0147-44a8-b0be-fc2ddfccd732', '1b2d617c-cb23-4db6-8038-e27792f6df40'),
    ('a9846c33-0147-44a8-b0be-fc2ddfccd732', '4a3adf62-f0c3-42eb-b247-26c572994c87')
ON CONFLICT (client_id, api_resource_id) DO NOTHING;