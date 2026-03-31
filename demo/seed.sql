-- ============================================================
-- NexusAuth Demo Seed Data
-- Run schema.sql FIRST to create all tables, then run this script.
--   psql -U postgres -f demo/schema.sql
--   psql -U postgres -d nexusauth -f demo/seed.sql
-- ============================================================

-- Set search_path so we don't need to prefix every table
SET search_path TO nexusauth;

-- 1. Seed a demo OAuth client
--    client_id:     demo-app
--    client_secret: demo-app-secret  (plaintext, BCrypt hash below)
--    redirect_uri:  http://localhost:5010/callback
INSERT INTO oauth_clients (id, client_id, client_secret_hash, client_name, description, redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'demo-app',
    '$2a$12$GZTaHz9ElEsFWjbfpZlII.QsFwJLKIMlYcGCCEka5yErs7dFe.c1C',
    'Demo Application',
    'A demo front-end/back-end separated application for testing NexusAuth OAuth2 SSO',
    '["http://localhost:5010/callback"]',
    '["openid","profile"]',
    '["authorization_code"]',
    true,
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secret_hash = EXCLUDED.client_secret_hash,
    redirect_uris = EXCLUDED.redirect_uris,
    allowed_scopes = EXCLUDED.allowed_scopes,
    allowed_grant_types = EXCLUDED.allowed_grant_types;

-- 2. Seed a demo user
--    username: demo
--    password: demo123  (plaintext, BCrypt hash below)
INSERT INTO users (id, username, password_hash, email, nickname, gender, is_active, created_at, updated_at)
VALUES (
    'b0000000-0000-0000-0000-000000000001',
    'demo',
    '$2a$12$SbcsOORAKGmnHG33dx6Th.Knp84X1RWncD6VxUJrhtTHvYbp6BnTK',
    'demo@nexusauth.local',
    'Demo User',
    0,
    true,
    NOW(),
    NOW()
)
ON CONFLICT (username) DO UPDATE SET
    password_hash = EXCLUDED.password_hash,
    email = EXCLUDED.email;

-- 3. Seed API resources for the demo scopes
INSERT INTO api_resources (id, name, display_name, description, is_active, created_at)
VALUES
    ('c0000000-0000-0000-0000-000000000001', 'openid', 'OpenID', 'OpenID Connect scope', true, NOW()),
    ('c0000000-0000-0000-0000-000000000002', 'profile', 'Profile', 'User profile scope', true, NOW())
ON CONFLICT DO NOTHING;

-- ============================================================
-- After running this script:
--   Terminal 1: cd src/NexusAuth.Host && dotnet run        (port 5100)
--   Terminal 2: cd demo/DemoApp.Api && dotnet run          (port 5010)
--   Open http://localhost:5010 in your browser
--   Click "Login with NexusAuth"
--   Login with: demo / demo123
-- ============================================================
