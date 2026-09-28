# Workbench API：接入 NexusAuth 统一登录

这是仓库中 Workbench API 的实现模板，适合需要在服务端保存 OAuth 凭据、由浏览器使用本地会话的 .NET 应用。它不是 NexusAuth 强制要求的客户端框架；若已有 OIDC 客户端库，也可以按同一协议接入。代码见 [`admin/src/NexusAuth.Workbench.Api`](../admin/src/NexusAuth.Workbench.Api) 和 [`admin/src/NexusAuth.Extension`](../admin/src/NexusAuth.Extension)。协议字段和安全边界以[主 README](../README.md)为准。

## 先登记客户端

在 NexusAuth 登记一个机密客户端和 API 服务资源。Workbench 自身由启动初始化器完成这一步；其他应用可通过管理界面登记，不应复用 `nexusauth.workbench` 或它的密钥。

| 项目 | 本例 | 新应用需要替换为 |
|---|---|---|
| Client ID | `nexusauth.workbench` | 应用自己的稳定 ID |
| 认证方式 | `client_secret_basic` | 与客户端登记值一致的方式 |
| Grant | `authorization_code`、`refresh_token` | 实际需要的 grant |
| PKCE | S256，必需 | 建议保持启用 |
| Scope | `openid profile offline_access nexusauth.workbench.api` | 身份 scope 与目标 API scope |
| Audience | `nexusauth.workbench.api` | 目标 API 的 audience |
| 回调 | `/signin-oidc` | 服务端实际处理回调的公开 URL |
| 登出回调 | Dashboard 根地址 | 已登记且逐字符匹配的 URL |

Workbench API 从 `Auth:Authority` 读取 Provider 公开 Issuer，必须与 Discovery 和 token 的 `iss` 一致。`Auth:ClientSecret` 只放服务端配置或 Secret。`Auth:Audience` 须与所请求服务资源的 audience 一致。根目录 Compose 将 `WORKBENCH_CLIENT_SECRET` 注入 API；独立部署可使用 `NEXUSAUTH_WORKBENCH_AUTH_CLIENT_SECRET` 等单下划线变量，完整清单在[快速启动](./01-快速启动.md#44-workbench-api认证和自身初始化)。

## 服务端登录链路

1. [`AuthController.Login`](../admin/src/NexusAuth.Workbench.Api/Controllers/AuthController.cs) 读取 NexusAuth Discovery，生成 `state`、`nonce`、S256 PKCE verifier/challenge，把状态暂存服务端，返回 `authorizeUrl`。浏览器只拿到授权地址，不拿 verifier 或客户端密钥。
2. 浏览器跳到 `/connect/authorize`。NexusAuth 登录、授权后，按登记的 `redirect_uri` 回到 `GET /signin-oidc`。模板的回调只接受 query；不要将客户端改成 `response_mode=form_post` 后仍沿用这个 GET 处理器。
3. API 消费 `state`，用 verifier 和 `client_secret_basic` 向 `/connect/token` 兑换授权码，按 JWKS 校验 ID Token 的签名、issuer、audience、时效和 nonce。失败时不签发本地会话。
4. 成功后，API 把用户身份和令牌保存在受保护的 `.NexusAuth.Workbench` HttpOnly Cookie 中，跳转 Dashboard 的 `/auth/callback`。浏览器不会直接收到 access token、refresh token 或客户端密钥。

可复用的协议客户端在 [`NexusAuth.Extension`](../admin/src/NexusAuth.Extension) 中，由 `AddNexusAuth` 注册。它提供 Discovery、PKCE、兑换、校验、刷新和 introspection，但不会自动添加登录端点、Cookie 或前端路由；这些由接入方自行实现。仓库内项目引用方式及注册示例见[扩展用法](./01-快速启动.md#96-在其他-net-应用中复用-workbench-扩展)。

## 验证每次请求与续期

默认模式下，管理 API 在没有 Bearer 头时使用 Workbench Cookie；携带 `Authorization: Bearer` 时交给 JWT Bearer 验证。Bearer 校验 issuer、audience、有效期，并要求 `token_use=access_token`；ID Token 不能用于调用 API。

[`WorkbenchCookieAuthenticationEvents`](../admin/src/NexusAuth.Workbench.Api/WorkbenchCookieAuthenticationEvents.cs) 在 Cookie 验证时检查保存的 access token：距离过期超过一分钟时通过 introspection 确认令牌仍有效且用户、客户端匹配；接近过期时刷新并保存轮换后的 access/refresh token。失败则拒绝并清除本地 Cookie。刷新后本地会话续到新的 24 小时窗口；这不是 Provider 登录页“几天内免登录”的配置。多副本部署须共享 Data Protection key ring，否则其他实例无法解开 Cookie。

登出由 `POST /api/auth/logout` 清本地 Cookie。如果配置 `SignOutProvider=true` 且有 ID Token，API 还返回 `/connect/endsession` 地址供浏览器继续跳转；Provider 会要求确认并撤销该用户的 SSO 会话和令牌。只清本地 Cookie 不等于结束 NexusAuth 的全局登录。

## 网关模式不是这套模板

`NEXUSAUTH_WORKBENCH_GATEWAY_ENABLED=true` 时，API 仅验证网关转发的 Bearer access token，登录、回调、Cookie、刷新和登出端点不注册；`/api/auth/me` 保留。网关必须自己处理 OIDC 会话和前端入口。根目录 Dashboard 不会随这个开关自动切换登录方式，不能只改 API 配置就直接使用。详见[快速启动的网关模式边界](./01-快速启动.md#44-workbench-api认证和自身初始化)。

## 对照实现

- [`AuthController`](../admin/src/NexusAuth.Workbench.Api/Controllers/AuthController.cs)：授权跳转、回调、退出。
- [`CurrentIdentityController`](../admin/src/NexusAuth.Workbench.Api/Controllers/CurrentIdentityController.cs)：`GET /api/auth/me`。
- [`WorkbenchApiModule`](../admin/src/NexusAuth.Workbench.Api/WorkbenchApiModule.cs)：Cookie/Bearer 策略、Audience 和网关模式。
- [`WorkbenchCookieAuthenticationEvents`](../admin/src/NexusAuth.Workbench.Api/WorkbenchCookieAuthenticationEvents.cs)：introspection 与 refresh token 轮换。
- [Workbench 项目 README](../admin/README.md)：项目启动、配置和管理接口。完整生产部署仍需处理 HTTPS、代理、Secret 和 Data Protection，不要照搬开发值。
