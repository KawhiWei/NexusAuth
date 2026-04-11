# 配置 OAuth 客户端

本文档介绍如何在 NexusAuth 中配置 OAuth 客户端应用。

## 配置方式

NexusAuth 支持两种方式配置 OAuth 客户端：

1. **SQL 脚本**：使用 seed.sql（推荐）
2. **数据库管理工具**：手动插入

## 使用 seed.sql 配置

### 创建 seed.sql 文件

参考 `src/NexusAuth.Workbench.Api/seed.sql`：

```sql
-- ============================================================
-- OAuth Client 配置示例
-- ============================================================

SET search_path TO nexusauth;

-- ============================================================
-- API Resources (Scopes)
-- ============================================================
INSERT INTO api_resources (id, name, display_name, audience, description, is_active, created_at)
VALUES
    ('10000000-0000-0000-0000-000000000101', 'openid', 'OpenID', 'openid', 'Standard OpenID Connect scope', true, NOW()),
    ('10000000-0000-0000-0000-000000000102', 'profile', 'Profile', 'profile', 'User profile information', true, NOW()),
    ('10000000-0000-0000-0000-000000000103', 'workbench', 'Workbench API', 'workbench', 'NexusAuth Workbench API scope', true, NOW())
ON CONFLICT (name) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    audience = EXCLUDED.audience;

-- ============================================================
-- OAuth Client 配置
-- ============================================================
INSERT INTO oauth_clients (id, client_id, client_secrets, token_endpoint_auth_method, client_name, description, redirect_uris, post_logout_redirect_uris, allowed_scopes, allowed_grant_types, require_pkce, is_active, created_at)
VALUES (
    '20000000-0000-0000-0000-000000000201',
    'NexusAuth.Workbench',  -- ClientId
    jsonb_build_array(
        jsonb_build_object(
            'Type', 'shared_secret',
            'Value', '$2a$12$...',  -- BCrypt 加密的密钥
            'Description', 'Workbench client secret'
        )
    ),
    'client_secret_basic',  -- 认证方式: client_secret_basic 或 none
    'NexusAuth Workbench',
    'NexusAuth Workbench Dashboard and API',
    '["http://localhost:5051/signin-oidc"]',  -- 回调地址
    '["http://localhost:5273/"]',  -- 登出回调地址
    '["openid", "profile", "workbench"]',  -- 授权 scope
    '["authorization_code", "refresh_token"]',  -- 授权模式
    true,  -- 是否要求 PKCE
    true,
    NOW()
)
ON CONFLICT (client_id) DO UPDATE SET
    client_secrets = EXCLUDED.client_secrets;

-- ============================================================
-- Client API Resource Mapping
-- ============================================================
INSERT INTO client_api_resources (client_id, api_resource_id)
VALUES
    ('20000000-0000-0000-0000-000000000201', '10000000-0000-0000-0000-000000000101'),
    ('20000000-0000-0000-0000-000000000201', '10000000-0000-0000-0000-000000000102'),
    ('20000000-0000-0000-0000-000000000201', '10000000-0000-0000-0000-000000000103')
ON CONFLICT (client_id, api_resource_id) DO NOTHING;
```

### 执行 seed.sql

```bash
psql -U nexusauth -d nexusauth -f src/NexusAuth.Workbench.Api/seed.sql
```

## 配置字段说明

| 字段 | 必填 | 说明 |
|------|------|------|
| client_id | 是 | 客户端唯一标识 |
| client_secrets | 否 | 客户端密钥（JSON 数组） |
| token_endpoint_auth_method | 是 | 认证方式：none, client_secret_basic, client_secret_post, private_key_jwt |
| client_name | 是 | 客户端显示名称 |
| redirect_uris | 是 | 授权回调地址（JSON 数组） |
| post_logout_redirect_uris | 否 | 登出回调地址（JSON 数组） |
| allowed_scopes | 是 | 允许的 scope（JSON 数组） |
| allowed_grant_types | 是 | 允许的授权模式（JSON 数组） |
| require_pkce | 是 | 是否强制使用 PKCE |
| is_active | 是 | 是否启用 |

## 生成 BCrypt 密钥

```csharp
// C# 生成
using BCrypt.Net;
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("your-secret-key", 12));
```

```javascript
// Node.js 生成
const bcrypt = require('bcrypt');
bcrypt.hash('your-secret-key', 12).then(hash => console.log(hash));
```

## 认证方式说明

### none（公共客户端）

适用于单页面应用或移动应用，不需要客户端密钥：

```sql
'client_secret' -> 'none'
```

### client_secret_basic（推荐）

适用于有后端的服务：

```sql
'client_secret' -> 'client_secret_basic'
```

### private_key_jwt

适用于高安全要求的场景，需要配置RSA密钥：

```sql
'client_assertion_type' -> 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer'
'client_assertion' -> 'Base64编码的JWT'
```

## 常见问题

### redirect_uri 不匹配

确保 OAuth 客户端配置的 redirect_uri 与应用配置的完全一致，包括协议、端口和路径。

### require_pkce 为 false

生产环境建议保持 require_pkce 为 true，除非有特殊需求。

## 下一步

- [对接 NexusAuth.Workbench.Api](./06-对接NexusAuth.Workbench.Api.md)