## Context

NexusAuth 当前已经不再只是“基础类库实现”，而是一个完整的 OAuth2.0 / OIDC 认证系统。项目结构现状如下：

- `NexusAuth.Host`
  - OAuth / OIDC 端点
  - 登录页、Consent 页、Device 页
  - Cookie 登录会话
- `NexusAuth.Application`
  - 客户端认证、授权码流程、Token 签发、Device Flow、Refresh Token 轮换
- `NexusAuth.Domain`
  - 聚合根、实体、Repository 接口
- `NexusAuth.Persistence`
  - Repository 实现、EF Core 映射、PostgreSQL 持久化
- `demo/`
  - 两套浏览器登录 demo
  - 多个控制台 grant demo

因此，这份设计文档需要从“仅核心库设计”更新为“完整系统设计”。

## Goals

### Goals

- 提供完整可运行的 OAuth2.0 / OIDC Authorization Server
- 支持浏览器交互登录与 BFF 模式
- 支持机器到机器调用
- 支持设备授权模式
- 支持 refresh token 轮换
- 同时支持：
  - `client_secret_basic`
  - `client_secret_post`
  - `private_key_jwt`
- 形成一套可演示、可对接外部客户端、可继续生产化演进的基础能力

### Non-Goals

- 当前仍未实现第三方社交登录（QQ / 微信 / GitHub）
- 当前仍未实现完整的租户隔离模型
- 当前未引入第三方 OAuth Server 框架（OpenIddict / IdentityServer / Duende）

## Architecture Decisions

### 1. 手工实现 OAuth2 / OIDC 协议逻辑

**Decision**
- 继续手工实现 OAuth2 / OIDC 协议逻辑，而不是引入第三方认证服务器框架。

**Why**
- 与 Luck 框架和现有分层架构更容易融合
- 可完全控制数据模型、协议细节与扩展点
- 更适合继续演进 `private_key_jwt`、device flow、BFF 自动 refresh 等自定义需求

### 2. 采用 Host + Application + Domain + Persistence 的 DDD 分层

**Decision**
- `Host` 只承载 HTTP 端点与交互页
- `Application` 承载协议编排与业务流程
- `Domain` 承载实体、聚合根与仓储接口
- `Persistence` 实现仓储接口并对接 PostgreSQL

**Why**
- 保持高内聚、低耦合
- 有利于后续继续加 demo、管理端、第三方登录适配器

### 3. 客户端认证方式采用“按 token_endpoint_auth_method 分发”

**Decision**
- 每个 client 通过 `token_endpoint_auth_method` 决定认证方式
- 当前支持：
  - `client_secret_basic`
  - `client_secret_post`
  - `private_key_jwt`

**Why**
- 可以让同一个 `NexusAuth.Host` 同时承载多种类型客户端
- 更适合 Web、机器、设备、Grafana 等多场景接入

### 4. 客户端凭据统一抽象为 `client_secrets`

**Decision**
- 不再使用单一 `ClientSecretHash`
- 统一改成 `client_secrets jsonb`
- 当前类型：
  - `shared_secret`
  - `jwks`

**Why**
- 预留扩展能力
- 与 `client_secret` 与 `private_key_jwt` 双模式共存更匹配

### 5. `private_key_jwt` 增加生产化最小安全收敛

**Decision**
- 只允许 `RS256`
- 校验 `iss == sub == client_id`
- 校验 `aud == endpoint`
- 要求 `jti`
- 对 `jti` 做防重放记录

**Why**
- 避免 demo 虽能用，但完全不具备上线基础

### 6. Refresh Token 采用轮换模型

**Decision**
- 每次 refresh 成功：
  - 吊销旧 refresh token
  - 生成新 refresh token
  - 生成新 access token

**Why**
- 更符合生产实践
- 降低 refresh token 重用风险

### 7. BFF 自动续期放在 Cookie ValidatePrincipal 事件中

**Decision**
- 不在每个 Controller 里手工刷新 access token
- 统一放到 `CookieAuthenticationEvents.ValidatePrincipal`

**Why**
- 更贴近 ASP.NET Core 标准模式
- 保持 BFF Controller 简洁
- 前端无感知自动续期

## High-Level Architecture

系统当前已经形成两个视角：

### 协议角色视角
- Resource Owner
- OAuth Client
- Authorization Server
- Resource Server

### DDD 分层视角
- Client Layer
- Presentation Layer (`NexusAuth.Host`)
- Application Layer (`NexusAuth.Application`)
- Domain Layer (`NexusAuth.Domain`)
- Infrastructure Layer (`NexusAuth.Persistence`)
- Storage Layer (`PostgreSQL`, signing key storage)

对应图文件：

- `architecture-overview.mmd`
- `architecture-layered.mmd`

## Data Model

当前系统实际落地的数据模型已经扩展为：

- `users`
- `oauth_clients`
- `api_resources`
- `client_api_resources`
- `authorization_codes`
- `refresh_tokens`
- `device_authorizations`
- `token_blacklist_entries`

其中关键差异点：

### `oauth_clients`
- `client_secrets jsonb`
- `token_endpoint_auth_method`
- `post_logout_redirect_uris`

### `device_authorizations`
- device flow 状态机持久化

### `token_blacklist_entries`
- access token 吊销黑名单
- `private_key_jwt` 的 `jti` 防重放记录

## Operational Notes

### Demo 环境
- 使用 `demo/schema.sql` + `demo/seed.sql`

### 生产初始化
- 使用 `production-init.sql`
- 不带 demo client / demo user

### 当前测试配置
- `access_token = 1 分钟`
- `refresh_token = 3 分钟`
- `BFF cookie = 3 分钟`

目的是更方便验证 refresh token 自动续期链路。

## Residual Risks

- 当前 demo 私钥仍存放在仓库中，仅适合演示
- 生产环境仍需：
  - Secret 管理
  - 多 key 轮换
  - 更完整审计日志
  - DataProtection 多副本共享
