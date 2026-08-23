# 配置 OAuth 客户端

本文介绍如何登记一个能安全接入 NexusAuth 的 OAuth 2.0/OIDC 客户端，并给出授权码、PKCE、token endpoint 客户端认证和 BFF 的最小示例。

示例中的 `YOUR_*` 和 `REPLACE_ME` 都是占位符。不要把真实生产密钥、JWT 私钥或 refresh token 写进文档、前端代码或 Git。

## 1. 客户端配置清单

在 Workbench 的“应用管理”中创建应用，或使用数据库 seed。至少需要明确以下字段：

| 字段 | 说明 |
|------|------|
| `client_id` | 应用的稳定唯一标识。不要使用可猜测的用户密码。 |
| `client_name` | 登录页和管理端展示名称。 |
| `redirect_uris` | 授权回调地址，必须与请求中的 `redirect_uri` 完全一致，包括协议、主机、端口、路径和大小写。 |
| `post_logout_redirect_uris` | 可选的登出回调地址，同样要求精确匹配。 |
| `allowed_scopes` | 允许请求的 scope，例如 `openid profile workbench offline_access`。应按最小权限配置。 |
| `allowed_grant_types` | 允许的 grant，例如 `authorization_code`、`refresh_token`、`client_credentials` 或 `device_code`。 |
| `require_pkce` | `true` 时授权请求必须有 PKCE；生产 Web/BFF 推荐保持为 `true`。 |
| `token_endpoint_auth_method` | 选择一种客户端认证方式，不能把多种方式当作同一次请求的备用凭据。 |
| `jwks` / `jwks_uri` | `private_key_jwt` 客户端的公钥配置。当前服务端验签使用已登记的内嵌 `jwks`，不会动态抓取 `jwks_uri`。 |

### 推荐的 Web/BFF 配置

新建 Web/BFF 应用时，建议使用：

```text
allowed_grant_types = ["authorization_code", "refresh_token"]
require_pkce = true
token_endpoint_auth_method = "client_secret_basic"
allowed_scopes = ["openid", "profile", "offline_access", "YOUR_API_SCOPE"]
```

`offline_access` 不是自动附加的 scope。只有客户端允许它、授权请求申请它，且登录同意后，token 响应才会包含 refresh token。

### 数据库配置示例

下面只展示关键字段。实际建库和列名以仓库中的 `production-init.sql`、`seed.sql` 为准；`client_secret` 不要直接写明文。

```sql
UPDATE oauth_clients
SET redirect_uris = '["https://app.example.com/signin-oidc"]',
    post_logout_redirect_uris = '["https://app.example.com/"]',
    allowed_scopes = '["openid","profile","offline_access","orders"]',
    allowed_grant_types = '["authorization_code","refresh_token"]',
    require_pkce = true,
    token_endpoint_auth_method = 'client_secret_basic',
    is_active = true
WHERE client_id = 'orders-bff';
```

共享密钥应由 Workbench 的凭据功能生成并通过 Secret 注入应用。生产环境不要复制仓库中 Demo 的默认 secret。

## 2. PKCE 配置和行为

NexusAuth 的授权码流程只接受 `code_challenge_method=S256`，不接受 `plain`。是否必须提交 `code_challenge` 由客户端的 `require_pkce` 决定：

| `require_pkce` | 授权请求 | 换 token 时的规则 |
|---|---|---|
| `true` | 必须同时提供 `code_challenge` 和 `code_challenge_method=S256`。缺少或方法不支持时返回 `invalid_request`。 | 必须提供匹配的 `code_verifier`，否则返回 `invalid_grant`。 |
| `false`，未携带 challenge | 允许不使用 PKCE。 | 可以不提供 `code_verifier`。只建议可信的机密后端使用。 |
| `false`，携带 S256 challenge | 仍然会记录并校验 PKCE。 | 必须提供匹配的 `code_verifier`。 |
| 任意值，携带 `plain` 或其他方法 | 拒绝请求。 | 不会降级到明文 verifier。 |

生成码对时，`code_verifier` 只保存在客户端会话，`code_challenge` 才放进授权 URL：

```bash
verifier='REPLACE_WITH_RANDOM_43_TO_128_CHAR_VALUE'
# challenge = BASE64URL(SHA256(verifier))
```

生产环境应使用密码学安全随机数生成器，而不是固定字符串。`state` 和 OIDC 的 `nonce` 也应每次登录随机生成，并在回调时校验。

## 3. 授权 URL 和 response_mode

下面是 `response_mode=query` 的示例。请把 `redirect_uri`、`state`、`nonce` 和 `code_challenge` 做正确的 URL 编码：

```text
http://localhost:5100/connect/authorize?response_type=code&client_id=orders-bff&redirect_uri=https%3A%2F%2Fapp.example.com%2Fsignin-oidc&scope=openid%20profile%20offline_access%20orders&state=RANDOM_STATE&nonce=RANDOM_NONCE&code_challenge=S256_CHALLENGE&code_challenge_method=S256&response_mode=query
```

登录成功后，Provider 会把浏览器重定向到：

```text
https://app.example.com/signin-oidc?code=AUTHORIZATION_CODE&state=RANDOM_STATE
```

如果使用 `response_mode=form_post`：

```text
http://localhost:5100/connect/authorize?response_type=code&client_id=orders-bff&redirect_uri=https%3A%2F%2Fapp.example.com%2Fsignin-oidc&scope=openid%20profile%20orders&state=RANDOM_STATE&nonce=RANDOM_NONCE&code_challenge=S256_CHALLENGE&code_challenge_method=S256&response_mode=form_post
```

Provider 会返回一个自动提交的 HTML 表单，将 `code`、`state`（或错误字段）以 `application/x-www-form-urlencoded` POST 到已登记的回调地址。使用 `form_post` 时，BFF 回调必须接受 POST；使用默认的 `query` 时，回调接受 URL 查询参数。

## 4. 换取 token

### client_secret_basic

这是服务端 Web/BFF 的推荐方式。客户端凭据放在 HTTP Basic 头，业务参数放在表单：

```bash
curl -i -X POST 'http://localhost:5100/connect/token' \
  -u 'orders-bff:REPLACE_WITH_CLIENT_SECRET' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=authorization_code' \
  --data-urlencode 'client_id=orders-bff' \
  --data-urlencode 'code=REPLACE_WITH_AUTHORIZATION_CODE' \
  --data-urlencode 'redirect_uri=https://app.example.com/signin-oidc' \
  --data-urlencode 'code_verifier=REPLACE_WITH_ORIGINAL_VERIFIER'
```

Basic 认证已经包含 `client_id` 时，不要再提交不同的表单 `client_id`。服务端会拒绝认证来源冲突，而不会猜测应该使用哪一个。

### client_secret_post

仅在客户端确实需要表单认证时使用：

```bash
curl -i -X POST 'http://localhost:5100/connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=authorization_code' \
  --data-urlencode 'client_id=orders-bff' \
  --data-urlencode 'client_secret=REPLACE_WITH_CLIENT_SECRET' \
  --data-urlencode 'code=REPLACE_WITH_AUTHORIZATION_CODE' \
  --data-urlencode 'redirect_uri=https://app.example.com/signin-oidc' \
  --data-urlencode 'code_verifier=REPLACE_WITH_ORIGINAL_VERIFIER'
```

### client_secret_jwt

客户端使用登记的共享密钥签名一个短时效 JWT：

- Header：`alg=HS256`；
- `iss` 和 `sub`：客户端 ID；
- `aud`：`/connect/token` 的完整地址；
- `exp`：短时效时间；
- `jti`：每次请求唯一，用于防重放；
- 表单提交 `client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer` 和 `client_assertion`。

不要同时提交 `client_secret`、Basic 头或另一种断言。示例中的共享密钥只能作为占位符：

```text
client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer
client_assertion=REPLACE_WITH_HS256_ASSERTION
```

### private_key_jwt

客户端使用自己的 RSA 私钥签名 `RS256` JWT，Provider 使用注册时保存的内嵌 JWKS 验签。JWT 的 `iss`、`sub`、`aud`、`exp`、`jti` 要符合上面的约束；私钥只保存在客户端服务端。Workbench 自动生成凭据时，请只在安全位置保存返回的一次性私钥内容。

当前版本不会通过 `jwks_uri` 动态下载客户端公钥。如果需要使用 `private_key_jwt`，请把公钥 JWKS 登记到客户端的 `jwks` 字段，并确保 `kid` 与签名头一致。

## 5. Refresh token 滑动续期

授权请求申请 `offline_access`，并确保客户端的 grant types 包含 `refresh_token`：

```bash
curl -i -X POST 'http://localhost:5100/connect/token' \
  -u 'orders-bff:REPLACE_WITH_CLIENT_SECRET' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=refresh_token' \
  --data-urlencode 'client_id=orders-bff' \
  --data-urlencode 'refresh_token=REPLACE_WITH_REFRESH_TOKEN'
```

成功后会返回新的 access token 和新的 refresh token。客户端必须替换旧 refresh token，不要并发重复使用同一个旧 token。旧 token 被再次使用会失败；应用应在刷新失败时清理本地会话并重新登录。

Workbench BFF 的最小配置示例：

```json
{
  "Auth": {
    "Authority": "https://sso.example.com",
    "ClientId": "orders-bff",
    "ClientSecret": "从 Secret 注入，不要提交到仓库",
    "RedirectUri": "https://app.example.com/signin-oidc",
    "PostLogoutRedirectUri": "https://app.example.com/",
    "Scope": "openid profile offline_access orders",
    "RequireHttpsMetadata": true,
    "SignOutProvider": true
  }
}
```

React 只访问 BFF 自己的会话接口，不直接保存 access token 或 refresh token。BFF 应把令牌放在加密的 HttpOnly Cookie 或服务端会话中，并配置独立的 Data Protection key。

## 6. 常见错误

| 错误 | 含义和处理 |
|------|------|
| `invalid_scope` | scope 不在客户端的 `allowed_scopes`，或对应服务资源未启用。修改客户端配置或减少请求 scope。若回调地址已安全确认，授权端点会带上 `state` 回跳错误。 |
| `invalid_request` | 缺少或格式错误的参数，例如 `grant_type`、`redirect_uri`、`code_challenge` 或 `response_mode`。 |
| `unauthorized_client` | 客户端未允许当前 grant type。检查 `allowed_grant_types`。 |
| `unsupported_response_type` | 当前只支持 `response_type=code`。 |
| `invalid_client` | 客户端不存在、未启用、认证方式不匹配、凭据错误或认证来源冲突。响应为 HTTP 401，并带 `WWW-Authenticate`；检查 Basic、表单和 JWT 断言是否只使用一种。 |
| `invalid_grant` | 授权码过期/已使用、`redirect_uri` 不一致或 PKCE verifier 错误；重新开始授权流程。 |
| `invalid_token` | access token 无效、过期或不适用于 UserInfo。使用 refresh token 或重新登录。 |
| 不跳回客户端 | Provider 无法安全确认 `redirect_uri` 时不会重定向，直接在 Provider 页面返回错误。这是为了防止把错误发送到攻击者控制的地址。 |

## 7. 安全检查清单

- 生产环境使用 HTTPS，并把所有回调地址改为 HTTPS。
- `require_pkce=true`，仅在确实可信的机密后端上考虑关闭。
- `client_secret`、private key、refresh token 只放服务端 Secret/密钥管理系统。
- 每次登录生成新的 `state`、`nonce` 和 PKCE verifier，并在回调校验。
- 不要把令牌、授权码和客户端断言写入普通日志。
- 生产环境配置受管的 Provider X.509 签名证书，并保留证书轮换计划。
- 使用发现文档中的 endpoint，不要硬编码与部署环境不一致的地址。

下一步可阅读 [08-高级配置](./08-高级配置.md) 和 [11-OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md)。
