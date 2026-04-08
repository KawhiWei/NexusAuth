## Context

NexusAuth 是一个基于 .NET 9.0 / Luck 框架的统一认证中心项目，使用 PostgreSQL 数据库，遵循 DDD 分层架构（Domain / Application / Persistence / Shared）。项目当前已完成基础骨架，但没有任何认证逻辑。本次设计的目标是在不引入第三方 OAuth2 框架（如 OpenIddict、IdentityServer）的前提下，手工实现符合 RFC 6749 标准的 OAuth2.0 核心流程，为后续扩展第三方登录打下基础。

本次变更**不创建** Web API 宿主项目（`NexusAuth.Host`），仅实现领域层、应用服务层、持久化层。API 端点注册将在后续独立 change 中完成。

## Goals / Non-Goals

**Goals:**
- 实现 OAuth2.0 Authorization Code + PKCE 流程的完整领域逻辑
- 实现 Client Credentials 流程
- 实现 Refresh Token 流程与吊销
- 实现用户身份：账号/密码、手机号、邮箱三种凭证，BCrypt 密码哈希
- 实现 OAuth2 Client 注册与验证（ClientId、ClientSecretHash、RedirectUri、Scope、GrantType）
- 实现 JWT Access Token 颁发（RS256 或 HS256）
- 使用 `IEntityTypeConfiguration<T>` 完成所有实体的 EF Core 映射
- 通过 `Directory.Packages.props` 集中管理所有新增 NuGet 包版本

**Non-Goals:**
- 本次不实现 HTTP API 端点（`/connect/authorize`、`/connect/token` 等）
- 本次不实现 OpenID Connect Discovery 端点
- 本次不实现第三方登录（QQ、微信、GitHub）
- 本次不实现管理界面或 Client 自助注册 API
- 本次不引入 OpenIddict / IdentityServer / Duende 等第三方 OAuth2 框架

## Decisions

### 1. 手工实现 OAuth2 端点逻辑，不引入第三方框架

**决策**：所有 OAuth2 流程逻辑（授权码生成、PKCE 校验、Token 颁发）均手工实现于 Application 层。

**理由**：
- 项目基于 Luck 自有框架，与 OpenIddict / IdentityServer 的 DI 模型存在冲突风险
- 手工实现可完全控制数据模型与存储结构，便于后续扩展第三方登录适配器
- OAuth2 核心流程逻辑并不复杂，过度依赖框架反而增加维护成本

**备选方案**：OpenIddict（被排除，因其要求特定的 Entity 模型与 DI 注册方式，与 Luck 框架集成复杂）

---

### 2. JWT 签名算法：HS256（对称）

**决策**：使用 HMAC-SHA256（HS256）签名 Access Token，密钥通过配置注入。

**理由**：
- 当前为单体服务，无需非对称密钥分发
- 配置简单，后续可升级为 RS256（通过替换 `ITokenSigningCredentialsProvider` 实现）

**备选方案**：RS256（非对称）—— 后续如需跨服务验证 Token，可通过扩展点升级

---

### 3. 领域层定义仓储接口，Persistence 层实现

**决策**：在 `NexusAuth.Domain` 中定义 `IUserRepository` 等接口，在 `NexusAuth.Persistence` 中实现。

**理由**：
- 遵循 DDD 仓储模式，领域层不依赖 EF Core
- 符合 Luck 框架的模块化架构惯例

---

### 4. EF 映射使用 `IEntityTypeConfiguration<T>`，通过 `ApplyConfigurationsFromAssembly` 自动注册

**决策**：每个实体对应一个独立的配置类，`NexusAuthDbContext.OnModelCreating` 中调用 `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())` 统一注册。

**理由**：
- 现有 `DbContext.cs` 已有此模式，保持一致性
- 配置类职责单一，便于维护和审查

---

### 5. 密码哈希：BCrypt

**决策**：使用 `BCrypt.Net-Next` 进行密码哈希，work factor = 12。

**理由**：BCrypt 是业界标准密码哈希算法，自带盐值，抗彩虹表攻击。

---

### 6. Authorization Code 存储于数据库，使用后立即标记失效

**决策**：授权码持久化到 `authorization_codes` 表，使用后设置 `IsUsed = true`，过期时间 10 分钟。

**理由**：防止授权码重放攻击（RFC 6749 §10.5）。内存存储无法支持多实例部署。

## Data Model

> 所有表使用 PostgreSQL，主键为 `uuid`，时间类型为 `timestamptz`。

---

### `users` — 用户表

| 列名 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `id` | `uuid` | PK | 用户唯一标识 |
| `username` | `varchar(100)` | NOT NULL, UNIQUE | 登录账号 |
| `password_hash` | `varchar(256)` | NOT NULL | BCrypt 哈希密码（work factor=12） |
| `nickname` | `varchar(100)` | NOT NULL | 用户昵称（显示名称） |
| `gender` | `smallint` | NOT NULL, DEFAULT 0 | 性别：0=Unknown, 1=Male, 2=Female |
| `ethnicity` | `varchar(50)` | NULLABLE | 民族 |
| `email` | `varchar(256)` | NULLABLE, UNIQUE | 邮箱，可用于登录 |
| `phone_number` | `varchar(20)` | NULLABLE, UNIQUE | 手机号，可用于登录 |
| `is_active` | `boolean` | NOT NULL, DEFAULT true | 账号是否启用 |
| `created_at` | `timestamptz` | NOT NULL | 创建时间 |
| `updated_at` | `timestamptz` | NOT NULL | 最后更新时间 |

索引：`username`（unique）、`email`（unique, partial where not null）、`phone_number`（unique, partial where not null）

---

### `oauth_clients` — OAuth2 客户端表

| 列名 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `id` | `uuid` | PK | 内部唯一标识 |
| `client_id` | `varchar(128)` | NOT NULL, UNIQUE | OAuth2 协议中的 client_id |
| `client_secret_hash` | `varchar(256)` | NOT NULL | BCrypt 哈希后的 client_secret |
| `client_name` | `varchar(256)` | NOT NULL | 客户端显示名称（管理界面展示） |
| `description` | `text` | NULLABLE | 客户端描述 |
| `redirect_uris` | `jsonb` | NOT NULL, DEFAULT '[]' | 允许的回调地址列表 |
| `allowed_scopes` | `jsonb` | NOT NULL, DEFAULT '[]' | 允许申请的 scope 列表 |
| `allowed_grant_types` | `jsonb` | NOT NULL, DEFAULT '[]' | 允许的授权类型（`authorization_code`、`client_credentials`、`refresh_token`） |
| `require_pkce` | `boolean` | NOT NULL, DEFAULT true | 是否强制要求 PKCE（Authorization Code 流程） |
| `is_active` | `boolean` | NOT NULL, DEFAULT true | 客户端是否启用 |
| `created_at` | `timestamptz` | NOT NULL | 创建时间 |

索引：`client_id`（unique）

---

### `api_resources` — API 资源表

| 列名 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `id` | `uuid` | PK | 资源唯一标识 |
| `name` | `varchar(128)` | NOT NULL, UNIQUE | 资源标识符（对应 scope 名称，如 `orders.read`） |
| `display_name` | `varchar(256)` | NOT NULL | 管理界面展示名称 |
| `description` | `text` | NULLABLE | 资源描述 |
| `is_active` | `boolean` | NOT NULL, DEFAULT true | 是否启用 |
| `created_at` | `timestamptz` | NOT NULL | 创建时间 |

索引：`name`（unique）

**说明**：`api_resources.name` 与 OAuth2 scope 对应。OAuthClient 的 `allowed_scopes` 中的每一项均应在 `api_resources.name` 中存在（应用层校验，非 FK 约束）。管理界面通过此表展示可分配的 API 资源列表，供管理员为 OAuthClient 勾选授权。

---

### `client_api_resources` — 客户端与 API 资源关联表

| 列名 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `client_id` | `uuid` | NOT NULL, FK → `oauth_clients.id` | 关联的客户端 |
| `api_resource_id` | `uuid` | NOT NULL, FK → `api_resources.id` | 关联的 API 资源 |

主键：`(client_id, api_resource_id)` 联合主键

**说明**：此表记录某个 OAuthClient 被授权可访问哪些 API 资源。`oauth_clients.allowed_scopes` 字段存储实际 Token 中携带的 scope 字符串，`client_api_resources` 表用于管理界面的可视化展示与权限分配，两者保持同步。

---

### `authorization_codes` — 授权码表

| 列名 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `id` | `uuid` | PK | 内部唯一标识 |
| `code` | `varchar(256)` | NOT NULL, UNIQUE | 授权码值（URL-safe 随机字符串，≥32 字符） |
| `client_id` | `varchar(128)` | NOT NULL | 对应 `oauth_clients.client_id` |
| `user_id` | `uuid` | NOT NULL | 授权用户，对应 `users.id` |
| `redirect_uri` | `varchar(2048)` | NOT NULL | 本次授权请求的 redirect_uri |
| `scope` | `varchar(512)` | NOT NULL | 本次授权申请的 scope（空格分隔） |
| `code_challenge` | `varchar(256)` | NULLABLE | PKCE code_challenge |
| `code_challenge_method` | `varchar(10)` | NULLABLE | `S256` 或 `plain` |
| `is_used` | `boolean` | NOT NULL, DEFAULT false | 是否已被兑换（防重放） |
| `expires_at` | `timestamptz` | NOT NULL | 过期时间（创建后 10 分钟） |
| `created_at` | `timestamptz` | NOT NULL | 创建时间 |

索引：`code`（unique）、`expires_at`（普通索引，用于清理过期记录）

---

### `refresh_tokens` — 刷新令牌表

| 列名 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `id` | `uuid` | PK | 内部唯一标识 |
| `token` | `varchar(512)` | NOT NULL, UNIQUE | Refresh Token 值（URL-safe 随机字符串，≥64 字符） |
| `client_id` | `varchar(128)` | NOT NULL | 对应 `oauth_clients.client_id` |
| `user_id` | `uuid` | NOT NULL | 授权用户，对应 `users.id` |
| `scope` | `varchar(512)` | NOT NULL | 此 Token 携带的 scope |
| `is_revoked` | `boolean` | NOT NULL, DEFAULT false | 是否已吊销 |
| `expires_at` | `timestamptz` | NOT NULL | 过期时间（创建后 30 天） |
| `created_at` | `timestamptz` | NOT NULL | 创建时间 |

索引：`token`（unique）、`user_id`（普通索引，用于全量吊销）、`expires_at`（普通索引，用于清理）

---

### 实体关系概览

```
users ──────────────────────────────────────┐
                                             │ user_id (逻辑关联)
oauth_clients ──── client_api_resources ──── api_resources
      │                                      
      │ client_id (逻辑关联)                 
      ├── authorization_codes                
      └── refresh_tokens                     
```

所有跨表关联均为**应用层逻辑关联**，数据库不设 FOREIGN KEY 约束，以保持 PostgreSQL 写入性能与部署灵活性。

## Risks / Trade-offs

- **[Risk] HS256 密钥泄漏** → Mitigation：密钥通过环境变量/Secret 管理注入，不硬编码
- **[Risk] Authorization Code 表数据量增长** → Mitigation：后续添加定时清理过期记录的后台任务
- **[Risk] Refresh Token 无法强制下线所有设备** → Mitigation：当前实现支持单 Token 吊销；全局下线可通过后续扩展 `user_sessions` 表实现
- **[Trade-off] 手工实现 vs 框架**：手工实现增加了初期代码量，但避免了框架升级绑定与模型侵入问题
- **[Risk] EF 迁移在 CI/CD 中需要数据库连接** → Mitigation：迁移脚本通过 `dotnet ef migrations script` 生成 SQL 文件，与代码分离执行

## Migration Plan

1. 在 `Directory.Packages.props` 中新增包版本声明
2. 在各项目 `.csproj` 中引用新包（不指定版本，由 CPM 管理）
3. 实现 Domain 层聚合根与实体
4. 实现 Application 层服务接口与实现
5. 实现 Persistence 层 EF 配置与 DbSet
6. 数据库表结构由**手动 SQL 脚本**创建，不使用 EF Migrations；`Microsoft.EntityFrameworkCore.Design` 包不引入；`DbContext` 启动时不调用 `Database.Migrate()`

**回滚**：回退对应代码提交即可，数据库表若已手动创建可单独通过 `DROP TABLE` 回滚

## Open Questions

- JWT Access Token 过期时间是否需要可配置（当前建议默认 1 小时）？
- Refresh Token 过期时间建议值（当前建议默认 30 天）？
- 是否需要在本次实现 `id_token`（OpenID Connect）？（当前设计排除在外，作为后续 change）
