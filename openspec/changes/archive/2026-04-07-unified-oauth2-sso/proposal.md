## Why

NexusAuth 需要从一个“只有 Domain / Application / Persistence 基础骨架”的项目，演进成一个真正可运行、可集成、可演示的 OAuth2.0 / OpenID Connect 认证授权中心。

最初的设想主要集中在：

- 用户身份管理
- OAuth2 Client 管理
- Authorization Code / Client Credentials / Refresh Token 的基础领域逻辑
- PostgreSQL 持久化映射

但随着实现推进，项目实际已经扩展为一个完整可运行的认证系统，包含：

- `NexusAuth.Host` Web 宿主
- OAuth 2.0 + OIDC 标准端点
- 登录页 / Consent 页 / Device 验证页
- `private_key_jwt` 与 `client_secret_basic/post` 双模式客户端认证
- `device_code` 授权模式
- `refresh_token` 自动续期与轮换
- 多套 demo（Web + BFF、ClientSecret、ClientCredentials、DeviceCode、RefreshToken）
- 生产初始化 SQL 与完整 Mermaid 架构 / 调用流程图

因此，原始归档计划已经不能准确反映当前代码现实，需要同步修正文档，避免后续维护者误以为该变更只覆盖了“核心类库而不含 Host 与 demo”。

## What Changed

当前这项归档变更实际已经落地为以下完整能力：

- 新增 `NexusAuth.Host`，提供完整 OAuth2 / OIDC Web 宿主能力
- 提供 OAuth 2.0 标准端点：
  - `/connect/authorize`
  - `/connect/token`
  - `/connect/deviceauthorization`
  - `/connect/revocation`
  - `/connect/introspect`
- 提供 OIDC 标准端点：
  - `/.well-known/openid-configuration`
  - `/.well-known/jwks.json`
  - `/connect/userinfo`
  - `/connect/endsession`
- 新增浏览器交互页：
  - 登录页
  - Consent 页
  - Device 验证页
- 客户端认证能力从最初的 `client_secret` 扩展为：
  - `client_secret_basic`
  - `client_secret_post`
  - `private_key_jwt`
- 授权模式从最初的 3 类扩展为：
  - `authorization_code + PKCE`
  - `client_credentials`
  - `refresh_token`
  - `device_code`
- OIDC 能力已补齐到可对接 Grafana / BFF / 外部 OIDC Client 的程度
- demo 已形成两套浏览器登录模式：
  - `private_key_jwt` 版 Web + BFF
  - `client_secret_basic` 版 Web + BFF
- 增加了控制台 demo：
  - `Demo.ClientCredentials`
  - `Demo.DeviceCode`
  - `Demo.RefreshToken`
- 增加了：
  - `production-init.sql`
  - Mermaid 架构图与完整代码调用流程图

## Current Capabilities

### Core OAuth2 / OIDC

- `oauth2-core`
  - Authorization Code + PKCE（仅允许 `S256`）
  - Client Credentials
  - Refresh Token
  - Device Code

- `token-management`
  - Access Token 签发
  - Id Token 签发
  - Refresh Token 轮换
  - Access Token 吊销 / Introspection

- `client-management`
  - OAuthClient 管理
  - `shared_secret` 与 `jwks` 多类型客户端凭据
  - `client_secret_basic/post`
  - `private_key_jwt`

- `user-identity`
  - 用户注册与凭证校验
  - 用户画像字段（Nickname / Gender / Ethnicity）

- `api-resource-management`
  - API 资源注册
  - Client 与 API Resource 授权映射

### Host / Interaction

- `host-web-endpoints`
  - OAuth / OIDC 标准端点
  - 登录态 Cookie 管理
  - Consent 交互
  - Device 验证与批准

### Demo / Integration

- `demo-web-private-key-jwt`
- `demo-web-client-secret`
- `demo-client-credentials`
- `demo-device-code`
- `demo-refresh-token`

## Impact

- **新增项目**：`NexusAuth.Host`
- **新增 demo 项目**：
  - `Demo.Bff`
  - `Demo.Web`
  - `Demo.Bff.ClientSecret`
  - `Demo.Web.ClientSecret`
  - `Demo.ClientCredentials`
  - `Demo.DeviceCode`
  - `Demo.RefreshToken`
- **数据库结构**：从最初的 4 表扩展到包含：
  - `users`
  - `oauth_clients`
  - `api_resources`
  - `client_api_resources`
  - `authorization_codes`
  - `refresh_tokens`
  - `device_authorizations`
  - `token_blacklist_entries`
- **认证方式**：已不再局限于 `ClientSecretHash`，而是统一抽象为 `client_secrets`
- **文档与可视化**：新增 README、生产初始化 SQL、Mermaid 架构与调用流程图
