# NexusAuth

NexusAuth 是一个基于 ASP.NET Core 和 .NET 10 的 OAuth 2.0 / OpenID Connect（OIDC）认证授权服务。它为 Workbench、业务 Web 应用和服务间调用提供统一登录、令牌签发与令牌校验能力。

它当前最适合 Web/BFF、服务端应用、机器到机器调用和受限设备流程。浏览器 SPA、移动端或桌面端这类无法安全保存客户端凭据的 public client 目前不应直接接入；建议通过 BFF 保护客户端密钥，并由 BFF 负责授权码兑换和 refresh token 保管。

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/React-19-blue" alt="React 19">
  <img src="https://img.shields.io/badge/PostgreSQL-16-blue" alt="PostgreSQL 16">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT">
</p>

## 文档与手册

- [使用手册（中文）](./document/12-使用手册.md)：部署、管理台、OAuth/OIDC、SCIM 2.0 和生产运维的完整操作指引。
- [User Guide (English)](./docs/en/user-guide.md)：对应的英文使用手册。
- [专题文档目录](./document/README.md)：快速开始、数据库、客户端接入、配置、Demo 和协议设计。

## 能力概览

### OAuth 2.0 流程

- `authorization_code`：推荐的 Web/BFF 登录流程，支持按客户端配置 PKCE，当前只接受 `S256`。
- `client_credentials`：服务间调用，不代表用户身份。
- `device_code`：受限输入设备授权，遵循 RFC 8628 的轮询语义。
- `refresh_token`：在客户端允许 `refresh_token` 且授权请求包含 `offline_access` 时签发；刷新会轮换旧 refresh token。

授权端点支持 `response_mode=query` 和 `response_mode=form_post`。当客户端、回调地址和请求参数已经安全校验后，授权错误会回跳到已登记的 `redirect_uri`，并保留 `state`；无法确认回调地址安全时，服务只返回本地错误响应，不会把错误发送到不可信地址。

### OpenID Connect

- `/.well-known/openid-configuration`：发现文档。
- `/.well-known/jwks.json`：Provider 签名公钥。
- `/connect/userinfo`：按 access token 的 scope 返回用户信息。
- `id_token`：授权码兑换时在请求 `openid` scope 后签发。
- `/connect/endsession`：RP-Initiated Logout。

### 客户端认证

每个客户端在注册时选择一种 token endpoint 认证方式，单个请求必须使用与注册值一致的方式：

- `client_secret_basic`：HTTP Basic，适合服务端应用，通常是首选。
- `client_secret_post`：表单提交共享密钥，适合兼容旧客户端的场景。
- `client_secret_jwt`：使用共享密钥签名 `HS256` 客户端断言。
- `private_key_jwt`：使用客户端私钥签名 `RS256` 客户端断言，服务端使用登记的内嵌 JWKS 验签。

客户端认证信息冲突、Basic 头格式错误、表单认证与 JWT 断言混用等情况不会静默选择某一种凭据。认证失败返回 `401 Unauthorized`、OAuth `invalid_client`，并带 `WWW-Authenticate` 响应头。缺少普通业务参数（例如 `grant_type` 或 `code`）则属于 `invalid_request`。

### 安全边界

- `redirect_uri` 精确匹配，不支持通配符。
- 授权码一次性消费，并绑定 `client_id` 和 `redirect_uri`。
- PKCE 只接受 `S256`；是否必须提供 `code_challenge` 由客户端的 `require_pkce` 决定。
- `state` 用于客户端防 CSRF，`nonce` 用于 OIDC 登录请求关联。
- refresh token 与客户端绑定并轮换，令牌和授权码不应写入日志。
- Provider 签名支持本地开发自签名 X.509 PFX；生产环境应使用证书管理系统提供的签名证书。

## 当前明确不支持

以下扩展不应在接入方案中被当作已实现能力：

- OAuth 2.0 Pushed Authorization Requests（PAR）
- JWT-Secured Authorization Request（JAR）
- JWT Authorization Response Mode（JARM）
- DPoP
- OAuth mTLS / 证书绑定访问令牌
- 动态客户端注册
- CIBA
- `token_endpoint_auth_method=none` 的 public client

这些能力可以在后续面向开放第三方生态或 FAPI 的版本中单独设计；当前版本请使用已登记的机密客户端和 BFF。

## 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                   NexusAuth OAuth Provider                  │
│                       (Port: 5100)                          │
│  /connect/authorize  /connect/token  /account/login         │
└─────────────────────────────────────────────────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         │                  │                  │
         ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│     Your BFF    │ │  Machine Client │ │    Workbench    │
│  Web application │ │ client_credentials│ │  Dashboard + API│
└─────────────────┘ └─────────────────┘ └─────────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │  PostgreSQL  │
                     └──────────────┘
```

## 快速开始

### Docker Compose

需要 Docker、Docker Compose 和可用的镜像仓库网络。开发环境可以直接启动：

```bash
docker compose up --build
```

启动后访问：

- Workbench Dashboard：<http://localhost:5273>
- Workbench API：<http://localhost:5051>
- NexusAuth Provider：<http://localhost:5100>

Workbench 会跳转到 Provider 登录页，登录成功后回到 Dashboard。空数据库首次启动时，SQL 只创建数据库结构并登记 Workbench 默认客户端；SSO 随后根据环境变量幂等创建初始管理员，不会在重复启动时覆盖已有密码。

Compose 的本地默认管理员来自环境变量回退值：`admin / wzw0126..`。这仅用于本地开发；部署前必须显式配置以下变量，生产环境不得保留回退密码：

```bash
NEXUSAUTH_BOOTSTRAP_ADMIN_USERNAME=admin
NEXUSAUTH_BOOTSTRAP_ADMIN_PASSWORD=REPLACE_WITH_A_STRONG_PASSWORD
NEXUSAUTH_BOOTSTRAP_ADMIN_NICKNAME="System Admin"
NEXUSAUTH_BOOTSTRAP_ADMIN_EMAIL=admin@example.com
WORKBENCH_CLIENT_SECRET=REPLACE_WITH_A_LONG_RANDOM_SECRET
```

`WORKBENCH_CLIENT_SECRET` 是 `nexusauth.workbench` 系统 OAuth 客户端的唯一密钥来源。首次初始化数据库时会以该值生成 BCrypt 哈希，Workbench API 运行时也使用同一值认证；生产环境必须显式设置它。Compose 中的回退值仅用于本地开发，不能用于生产。

`demo/seed.sql` 不再由 Compose 自动执行。需要演示客户端与示例用户时，可以在本地开发数据库中手动执行该脚本。

### 数据库脚本职责

- [production-init.sql](./production-init.sql)：仅用于全新、空的 `nexusauth` 数据库，定义当前最终 schema；不删库、不含 `ALTER TABLE`，也不创建用户。
- `database/001_*.sql` 至 `database/006_*.sql`：仅用于将历史库按版本升级到当前结构，不能用于新库初始化。
- `admin/src/NexusAuth.Workbench.Api/seed.sql`：登记 Workbench 所需的 scope 和 OAuth 客户端；通过 psql 变量 `workbench_client_secret` 写入 `WORKBENCH_CLIENT_SECRET` 的 BCrypt 哈希。
- `demo/seed.sql`：只用于本地演示客户端和示例用户，禁止用于生产。

本地 Compose 默认使用 Development 环境自动生成并持久化开发签名证书。生产环境必须设置 `NEXUSAUTH_SSO_ENVIRONMENT=Production`，挂载由证书管理系统提供的 PFX，并通过 `NEXUSAUTH_SIGNING_CERTIFICATE_PATH` 和 Secret 配置证书密码。生产环境不会自动生成开发证书。

要重新初始化本地数据库（会删除 Compose 数据卷，请确认数据可丢失）：

```bash
docker compose down -v
docker compose up --build
```

### 本地运行

先准备 PostgreSQL 和数据库结构，再分别启动 Provider、Workbench API 和 Dashboard：

```bash
dotnet run --project src/NexusAuth.Host
dotnet run --project admin/src/NexusAuth.Workbench.Api

cd admin/src/NexusAuth.Workbench.Dashboard
npm install
npm run dev
```

也可以使用两个解决方案：`NexusAuth.sln` 负责 Provider 和共享后端项目，`admin/NexusAuth.Admin.sln` 负责 Workbench。详细步骤见 [快速开始](./document/01-快速开始.md) 和 [环境准备](./document/02-环境准备.md)。

### 日志记录

SSO 和 Workbench API 共用 `Luck.Logging.Serilog` 提供的 Serilog 配置。站点入口使用最新的 `AddLuckSerilog()` 扩展：

```csharp
using Luck.Logging.Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddLuckSerilog();

var app = builder.Build();
app.UseLuckRequestLogContext();
app.MapControllers();
```

该包负责注册 Serilog、配置 console/file sinks，并提供结构化字段的默认值。`UseLuckRequestLogContext()` 为每个 HTTP 请求建立覆盖完整请求生命周期的日志 scope，填充 `RequestTraceId`、`Filter1`、`Filter2`、`Module` 和 `Category`；`Subcategory` 按业务需要补充。请求结束由组件记录一次状态码、耗时、HTTP 方法和路径，未处理异常记录为 Error 后继续抛出。两个站点都会同时写控制台和文件，因此 Docker 日志仍然可以通过 `docker compose logs` 查看，文件日志则适合检索和长期保留。

每条日志都保持下面的固定格式。缺失字段保留为空列，不会改变列的位置：

```text
[时间][级别][模块][分类][子分类][TraceId][过滤1][过滤2][日志内容]
```

实际输出示例：

```text
[2026-08-23 14:20:10.123][INF][Authorize][Authorize][][c4b2...93a1][7f8d...11e0][user-1024][Request completed. StatusCode=200 ElapsedMs=18 Method=GET Path=/connect/authorize]
```

其中 `级别` 是独立的 `TRC`、`DBG`、`INF`、`WRN`、`ERR` 或 `FTL` 字段，不再拼在日志内容里。

MVC 请求使用控制器名作为 `Module`、Action 名作为 `Category`；`Subcategory` 是可选业务分类，请求完成日志中为空。业务日志可通过 `ApplicationLogScope.Begin(logger, subcategory, businessId, outcome)` 写入 `Authentication`、`ClientAuthentication`、`AuthorizationCode` 或 `Token` 等子分类。非 MVC Controller 端点使用 `HTTP` 作为模块，并以 Endpoint DisplayName 作为分类。站点启动日志没有控制器上下文，仍使用各站点的 `LuckLogging.Module`。

`RequestTraceId` 是独立的 W3C 分布式追踪标识，优先取 `Activity.Current.TraceId`，由标准 `traceparent` 在服务间传播。`Filter1` 在每个服务的请求入口直接生成一次，并在该请求内保持不变。`Filter2` 优先从当前用户的 `NameIdentifier` 或 `sub` Claim 获取；匿名请求没有用户 ID 时由组件生成请求级 GUID。`Filter1` 和 `Filter2` 不使用自定义 HTTP Header 传播。

业务层通过 `ApplicationLogScope.Begin(logger, subcategory, businessId, outcome)` 建立嵌套 scope：`Filter1` 始终继承请求入口的唯一 ID；业务代码明确掌握用户、客户端或其他业务主键时，可以用 `businessId` 补充当前业务日志的 `Filter2`；`outcome` 记录 `LoginSucceeded`、`InvalidPassword` 或 `RefreshTokenRotated` 等业务结果。未传入业务 ID 时继承请求级用户 ID。

这种格式借鉴了 SkyEye 日志的字段职责，但没有照搬完整 HTTP body、header、Cookie 或 SQL 内容。认证日志可能包含敏感上下文，默认只记录排查所需的稳定标识和结果码，绝不记录 password、client secret、authorization code、access token、refresh token、private key、assertion、verifier 或完整 claims/scope。需要查看请求细节时，应结合 TraceId 到受控的网关或审计系统中排查。

默认文件位置为：

- SSO：`logs/nexusauth-sso-YYYYMMDD.log`
- Workbench：`logs/nexusauth-workbench-YYYYMMDD.log`

文件按天滚动，单文件达到 100MB 时继续分片，每个站点保留最近 30 个文件，并启用共享写入和约 1 秒的磁盘刷新。默认最低级别为 `Information`。可以在各站点的 `LuckLogging` 配置节中覆盖 `Module`、`FilePath`、`MinimumLevel`、`MinimumLevelOverrides`、`FileSizeLimitBytes`、`RetainedFileCountLimit`、`RollOnFileSizeLimit`、`Shared` 和 `FlushIntervalSeconds`；环境变量使用双下划线，例如 `LuckLogging__MinimumLevel=Information`、`LuckLogging__MinimumLevelOverrides__Microsoft.AspNetCore=Information` 或 `LuckLogging__FilePath=/var/log/nexusauth/sso-.log`。

默认不会记录 `Information` 级别的 EF Core SQL 命令，避免查询参数长期落盘。确需短时间排查数据库问题时，可以把 `LuckLogging__MinimumLevelOverrides__Microsoft.EntityFrameworkCore.Database.Command` 调整为 `Information`，排查结束后应立即恢复为 `Warning`。

默认过滤规则把 `Microsoft`、`System` 和 `Npgsql` 的日志门槛设为 `Warning`，其中所有 `Microsoft.*` 分类只有 `Warning`、`Error` 和 `Fatal` 会被记录；NexusAuth 自定义分类不设置额外门槛，可以记录全部级别。业务日志覆盖注册、登录结果、客户端认证结果、授权码签发与消费、令牌签发、refresh token 轮换和撤销等高价值事件。

Docker Compose 会把宿主机的 `./logs/sso` 和 `./logs/workbench` 分别挂载到容器的 `/app/logs`。`logs/` 已加入 `.gitignore`，运行时文件不会提交到 Git。日志中不要写入 client secret、授权码、access token、refresh token、private key、密码或完整 Cookie；应用显式提供 `TraceId` 时可用它关联请求，需要排查身份时只记录脱敏后的 client id 或用户标识。

## 发现文档和最小接入

Provider 启动后先读取发现文档：

```bash
curl http://localhost:5100/.well-known/openid-configuration
```

接入前需要在 Workbench 的应用管理中登记：

- 唯一的 `client_id` 和客户端名称；
- 完全匹配的 `redirect_uris`；
- 可选的 `post_logout_redirect_uris`；
- `allowed_scopes` 和 `allowed_grant_types`；
- `require_pkce`；
- 一个 token endpoint 认证方式及对应凭据。

服务端 Web 应用建议选择 `client_secret_basic` + `require_pkce=true`。不要把 `client_secret`、private key 或 refresh token 放到 React、移动端或公开仓库中。

## 项目组成

### Provider 和共享项目（`src/`）

| 目录 | 说明 | 端口 |
|------|------|------|
| `src/NexusAuth.Host` | OAuth 2.0 / OIDC Provider | 5100 |
| `src/NexusAuth.Application` | SSO 与 Workbench 共享的应用服务 | - |
| `src/NexusAuth.Domain` | 领域模型 | - |
| `src/NexusAuth.Persistence` | EF Core 数据访问 | - |

### Workbench 管理端（`admin/`）

| 目录 | 说明 | 端口 |
|------|------|------|
| `admin/src/NexusAuth.Workbench.Api` | BFF 后端 | 5051 |
| `admin/src/NexusAuth.Workbench.Dashboard` | React 管理界面 | 5273 |
| `admin/src/NexusAuth.Extension` | Workbench OIDC 客户端扩展 | - |

Workbench 解决方案引用根目录的共享项目，不复制领域、应用或数据访问代码。

## 文档导航

| 文档 | 内容 |
|------|------|
| [文档总览](./document/README.md) | 能力、目录和完整导航 |
| [快速开始](./document/01-快速开始.md) | 开发环境和启动步骤 |
| [环境准备](./document/02-环境准备.md) | .NET、Node.js、PostgreSQL 和 Docker |
| [数据库配置](./document/03-数据库配置.md) | 数据库和 seed |
| [启动 Provider](./document/04-启动NexusAuth.Provider.md) | 单独启动 SSO |
| [配置 OAuth 客户端](./document/05-配置OAuth客户端.md) | 客户端、PKCE、认证方式、curl 与 BFF |
| [对接 Workbench](./document/06-对接NexusAuth.Workbench.md) | Workbench API 登录流程 |
| [启动 Dashboard](./document/07-对接NexusAuth.Workbench.Dashboard.md) | Workbench 前端 |
| [高级配置](./document/08-高级配置.md) | Token、证书、代理和安全配置 |
| [常见问题](./document/09-常见问题.md) | 常见错误和排查 |
| [Demo 详解](./document/10-Demo示例详解.md) | 仓库内 Demo |
| [协议设计](./document/11-OAuth-OIDC协议设计.md) | 验证顺序、错误决策和扩展边界 |

## Demo 客户端

| client_id | 认证方式 | grant types | 用途 |
|---|---|---|---|
| `demo-bff` | `private_key_jwt` | `authorization_code`, `refresh_token` | Web + BFF |
| `demo-bff-secret` | `client_secret_basic` | `authorization_code`, `refresh_token` | Web + BFF |
| `demo-cc` | `private_key_jwt` | `client_credentials` | 机器到机器 |
| `demo-device` | `private_key_jwt` | `device_code`, `refresh_token` | 设备授权 |

这些是本地演示配置，不代表生产密钥或生产部署模板。Demo 说明见 [Demo 示例详解](./document/10-Demo示例详解.md)。

## 生产注意事项

1. 使用 HTTPS，设置正确的可信反向代理和 `Secure` Cookie。
2. 将 Provider 签名证书、客户端密钥和 private key 放入 Secret、Vault 或 KMS，不要提交到 Git。
3. 多副本部署时共享 ASP.NET Core Data Protection key 和数据库连接。
4. 为登录、授权码、令牌签发、刷新、撤销和客户端变更加审计日志，但不要记录令牌原文。
5. 所有客户端使用精确回调地址，生产环境保持 `require_pkce=true`。
6. 把 PAR、JAR、JARM、DPoP、mTLS 等扩展列为未实现能力，接入前不要假设 Provider 会接受它们。

## 许可证

MIT License。
