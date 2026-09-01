# 配置 OAuth 客户端

本文说明如何登记业务应用并接入 NexusAuth 的 OAuth 2.0 / OpenID Connect 授权码流程。示例中的域名、客户端 ID 和密钥都是占位值，生产环境必须替换，并通过 Secret 管理。

当前 Provider 面向机密客户端。不要把 `client_secret`、JWT 私钥或 refresh token 放进浏览器 SPA、移动端或桌面端安装包；公开客户端请通过 BFF 接入。

## 1. 登记客户端

在 Workbench Dashboard 的“应用管理”中创建客户端。数据库字段的最终定义以 [production-init.sql](../production-init.sql) 为准，常用字段如下：

| 字段 | 说明 |
|------|------|
| `client_id` | 稳定且唯一的应用标识，不是用户密码。 |
| `client_name` | 登录页和管理端展示名称。 |
| `redirect_uris` | 授权回调地址。必须与授权请求的 `redirect_uri` 完全一致，包含协议、主机、端口、路径、查询串和大小写。 |
| `post_logout_redirect_uris` | 可选的登出回调地址，也必须精确匹配。只有带有效 `id_token_hint` 时才能使用。 |
| `allowed_scopes` | 允许请求的 scope。身份 scope 包括 `openid`、`profile`、`email`、`phone`、`address`、`offline_access`；业务 scope 来自 API resource 的 `audience`。 |
| `allowed_grant_types` | 允许的 grant：`authorization_code`、`refresh_token`、`client_credentials` 或 `urn:ietf:params:oauth:grant-type:device_code`。 |
| `require_pkce` | `true` 时授权请求必须携带 PKCE；生产 Web/BFF 建议保持为 `true`。 |
| `token_endpoint_auth_method` | Token endpoint 的客户端认证方式，只能使用登记的那一种。支持 `client_secret_basic`、`client_secret_post`、`client_secret_jwt` 和 `private_key_jwt`。 |
| `jwks` / `jwks_uri` | `private_key_jwt` 的公钥配置。当前 Provider 验签读取客户端登记的 `jwks`，不会动态下载 `jwks_uri`。 |

### 推荐的 Web/BFF 配置

```text
allowed_grant_types = ["authorization_code", "refresh_token"]
require_pkce = true
token_endpoint_auth_method = "client_secret_basic"
allowed_scopes = ["openid", "profile", "offline_access", "YOUR_API_SCOPE"]
```

`offline_access` 不会自动附加。客户端必须同时满足：客户端允许该 scope、授权请求申请该 scope，并使用允许的 `refresh_token` grant；这样授权码兑换响应才会包含 refresh token。

### 业务 API resource 与 scope

每个业务 scope 都必须对应一个 active API resource，其 `audience` 就是 access token 的 audience。请先在 Workbench 的“服务资源”中登记 API resource，再在“应用管理”中把它关联到客户端。创建或更新客户端时，Workbench 会将关联 resource 的 audience 合并进 `allowed_scopes`。

同一个 token 请求中的业务 scope 应属于同一个 audience；Provider 不会为跨多个 audience 的 scope 签发一个 token。`client_credentials` 只能请求业务 resource scope，不能请求 `openid`、`profile` 或 `offline_access`。

### 使用 SQL 修改客户端

手动 SQL 必须使用 `nexusauth` schema，且 JSON 字段应写成合法 JSON：

```sql
UPDATE nexusauth.oauth_clients
SET redirect_uris = '["https://app.example.com/signin-oidc"]'::jsonb,
    post_logout_redirect_uris = '["https://app.example.com/"]'::jsonb,
    allowed_scopes = '["openid","profile","offline_access","orders"]'::jsonb,
    allowed_grant_types = '["authorization_code","refresh_token"]'::jsonb,
    require_pkce = true,
    token_endpoint_auth_method = 'client_secret_basic',
    is_active = true
WHERE client_id = 'orders-bff';
```

`orders` 必须已经是 active API resource 的 `audience`，否则授权请求会返回 `invalid_scope`。共享密钥请使用 Workbench 的凭据功能生成，并只通过 Secret 注入应用；生产环境不要复制仓库中的 Demo 密钥。

## 2. PKCE

授权码流程只接受 `code_challenge_method=S256`，不接受 `plain`。`require_pkce` 决定是否必须提交 challenge：

| `require_pkce` | 授权请求 | 换 token 时 |
|---|---|---|
| `true` | 必须同时提供 `code_challenge` 和 `code_challenge_method=S256`，否则返回 `invalid_request`。 | 必须提供匹配的 `code_verifier`，否则返回 `invalid_grant`。 |
| `false`，不带 challenge | 允许不使用 PKCE，只适合可信的机密后端。 | 可以不提供 `code_verifier`。 |
| `false`，带 S256 challenge | 仍会记录并校验 PKCE。 | 必须提供匹配的 `code_verifier`。 |
| 任意值，带 `plain` 或其他方法 | 拒绝请求。 | 不会降级到明文 verifier。 |

`code_verifier` 只保存在客户端会话，`code_challenge` 才放进授权 URL。生产环境使用密码学安全随机数，并为每次登录生成新的 `state`、`nonce` 和 PKCE verifier，在回调时校验它们。

## 3. 发起授权

Provider 的授权端点是：

```text
GET http://localhost:5100/connect/authorize
```

示例使用默认的 `response_mode=query`：

```text
http://localhost:5100/connect/authorize?response_type=code&client_id=orders-bff&redirect_uri=https%3A%2F%2Fapp.example.com%2Fsignin-oidc&scope=openid%20profile%20offline_access%20orders&state=RANDOM_STATE&nonce=RANDOM_NONCE&code_challenge=S256_CHALLENGE&code_challenge_method=S256&response_mode=query
```

登录成功后，Provider 会将浏览器重定向到已登记的地址：

```text
https://app.example.com/signin-oidc?code=AUTHORIZATION_CODE&state=RANDOM_STATE
```

Provider 还支持 `response_mode=form_post`。此时会返回自动提交的 HTML 表单，将 `code`、`state` 或错误字段以 `application/x-www-form-urlencoded` POST 到回调地址；回调端点必须接受 POST。Workbench API 的内置回调使用 GET，因此不要把它配置为 `form_post`。

支持的 `prompt` 值为 `none`、`login` 和 `consent`；`max_age` 可要求最近一次认证时间。Provider 无法安全确认客户端或回调地址时不会重定向错误，避免把信息发送到未登记的地址。

## 4. 换取 token

Token endpoint 只接受 `application/x-www-form-urlencoded` POST：

```text
POST http://localhost:5100/connect/token
```

### `client_secret_basic`

服务端 Web/BFF 推荐使用 HTTP Basic。客户端 ID 和密钥放在 Basic 头中，业务参数放在表单：

```bash
curl -i -X POST 'http://localhost:5100/connect/token' \
  -u 'orders-bff:REPLACE_WITH_CLIENT_SECRET' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=authorization_code' \
  --data-urlencode 'code=REPLACE_WITH_AUTHORIZATION_CODE' \
  --data-urlencode 'redirect_uri=https://app.example.com/signin-oidc' \
  --data-urlencode 'code_verifier=REPLACE_WITH_ORIGINAL_VERIFIER'
```

Basic 头已经包含 `client_id`，不要再提交不同的表单 `client_id`。请求同时使用 Basic、`client_secret` 或 JWT assertion 时会被拒绝。

### `client_secret_post`

仅在确实需要表单认证时使用：

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

### `client_secret_jwt`

客户端使用 Workbench 生成的共享密钥签名短时效 JWT，并提交：

- Header `alg=HS256`；
- `iss` 和 `sub` 等于客户端 ID；
- `aud` 等于当前请求看到的 Token endpoint 完整地址；
- 必填 `exp` 和每次请求唯一的 `jti`；
- `client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`；
- `client_assertion`。

不要同时提交 Basic、`client_secret` 或另一种 assertion。Provider 会拒绝已使用过的 `jti`，防止 assertion 重放。

### `private_key_jwt`

客户端使用自己的 RSA 私钥签名 `RS256` JWT，Provider 使用客户端记录在 `jwks` 字段中的 RSA 公钥验签。JWT 的 `iss`、`sub`、`aud`、`exp` 和 `jti` 必须满足上述约束，私钥只保存在客户端服务端。

Workbench 可以生成一次性私钥和 JWKS；私钥只在安全位置保存。当前版本不会通过 `jwks_uri` 动态下载公钥；只配置 `jwks_uri` 不能完成当前 Provider 的验签。

## 5. Refresh token

授权码请求申请 `offline_access` 后，成功兑换会返回 refresh token。使用 refresh token 时，客户端仍必须使用登记的客户端认证方式：

```bash
curl -i -X POST 'http://localhost:5100/connect/token' \
  -u 'orders-bff:REPLACE_WITH_CLIENT_SECRET' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=refresh_token' \
  --data-urlencode 'refresh_token=REPLACE_WITH_REFRESH_TOKEN'
```

Provider 会轮换 refresh token：响应包含新的 access token 和新的 refresh token，客户端必须原子替换旧值，不要并发重复使用同一个旧 token。刷新响应不会返回新的 `id_token`；如果刷新失败，应清理本地会话并重新走授权流程。

## 6. Workbench BFF 最小配置

仓库内 Workbench API 使用以下配置结构。生产 Secret 不要写入 `appsettings.json`：

```json
{
  "Auth": {
    "Authority": "https://sso.example.com",
    "BackchannelAuthority": "http://sso:8080",
    "ClientId": "orders-bff",
    "ClientSecret": "从 Secret 注入",
    "RedirectUri": "https://app.example.com/signin-oidc",
    "PostLogoutRedirectUri": "https://app.example.com/",
    "Scope": "openid profile offline_access orders",
    "Audience": "orders",
    "RequireHttpsMetadata": true,
    "SignOutProvider": true
  }
}
```

`BackchannelAuthority` 只用于 BFF 到 Provider 的内部请求，浏览器可访问的 `Authority` 必须与 Provider 的 `Jwt:Issuer` 一致。Workbench API 将 access token 和 refresh token 保存在受保护的认证 Cookie 中，Dashboard 只调用 BFF 的 `/api` 接口，不直接保存令牌。

## 7. 错误和安全检查

| 错误 | 处理 |
|------|------|
| `invalid_scope` | 检查客户端是否允许该 scope，以及业务 scope 对应的 API resource 是否 active。 |
| `invalid_request` | 检查 `grant_type`、`redirect_uri`、`code_challenge`、`response_mode` 等参数。 |
| `unauthorized_client` | 检查客户端的 `allowed_grant_types`。设备授权的 grant 使用完整 URN。 |
| `unsupported_grant_type` | Provider 不支持请求的 grant。当前支持授权码、客户端凭据、refresh token 和设备码。 |
| `invalid_client` | 客户端不存在、未启用、认证方式不匹配、凭据错误或提交了多种认证方式。响应为 HTTP 401，并带 `WWW-Authenticate`。 |
| `invalid_grant` | 授权码过期/已使用、回调地址不一致、PKCE verifier 错误或 refresh token 无效；重新开始流程。 |
| `invalid_token` | access token 无效、过期或不适用于 UserInfo。刷新或重新登录。 |
| 不跳回客户端 | Provider 无法安全确认 `redirect_uri` 时会在 Provider 页面返回错误，这是预期的安全行为。 |

生产检查清单：

- 全部 Provider、回调和登出地址使用 HTTPS。
- Web/BFF 保持 `require_pkce=true`，并为每次登录校验 `state`、`nonce` 和 verifier。
- `client_secret`、private key、授权码、access token 和 refresh token 只放服务端 Secret 或密钥管理系统。
- 生产 Provider 使用受管的 X.509 签名证书或 RSA 私钥，并规划 `kid` 轮换。
- 使用 Discovery 返回的 endpoint，不要硬编码与部署环境不一致的地址。

下一步可阅读 [08-高级配置](./08-高级配置.md)、[11-OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md) 和仓库中的 `demo/` 示例。
