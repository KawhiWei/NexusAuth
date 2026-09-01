# 对接 NexusAuth.Workbench

Workbench 由两个独立服务组成：

- `NexusAuth.Workbench.Api`：OIDC BFF 和管理 API，默认端口 5051；
- `NexusAuth.Workbench.Dashboard`：React 管理前端，开发端口 5273，Compose 中由 nginx 提供静态文件并反向代理 `/api`。

Workbench API 使用授权码 + PKCE 接入 NexusAuth。浏览器只持有 Workbench 的受保护 Cookie，OAuth client secret、access token 和 refresh token 不应进入 Dashboard 或浏览器存储。

## 1. 启动内置 Workbench

推荐先按 [快速开始](./01-快速开始.md) 使用 Compose 启动全部服务。若分别运行进程，请确保 Provider 已启动、数据库已初始化，并先执行 Workbench seed：

```bash
psql -h localhost -U nexusauth -d nexusauth -v ON_ERROR_STOP=1 <<'SQL'
SET search_path TO nexusauth;
\i admin/src/NexusAuth.Workbench.Api/seed.sql
SQL
```

`seed.sql` 会登记 `openid`、`profile`、`workbench` API resource，以及 `nexusauth.workbench` OAuth client。它不写入共享密钥，也不创建用户。Workbench API 启动时会从 `Auth:ClientSecret` 读取密钥，并为该客户端创建或同步 BCrypt 保护的凭据。

在仓库根目录启动 API：

```bash
dotnet run --project admin/src/NexusAuth.Workbench.Api
```

API 默认监听 http://localhost:5051。Swagger 位于 http://localhost:5051/swagger。

再按 [Dashboard 文档](./07-对接NexusAuth.Workbench.Dashboard.md) 启动前端，或直接访问 Compose 的 http://localhost:5273。

## 2. 配置 Workbench API

内置 API 使用 `Auth` 配置节。下面是本地结构示例；生产环境的 client secret 应由 Secret 注入：

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=nexusauth;Username=nexusauth;Password=REPLACE_WITH_DATABASE_PASSWORD;Search Path=nexusauth"
  },
  "Auth": {
    "Authority": "http://localhost:5100",
    "BackchannelAuthority": "",
    "ClientId": "nexusauth.workbench",
    "ClientSecret": "REPLACE_WITH_WORKBENCH_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5051/signin-oidc",
    "PostLogoutRedirectUri": "http://localhost:5273/",
    "Scope": "openid profile workbench offline_access",
    "Audience": "workbench",
    "RequireHttpsMetadata": false,
    "SignOutProvider": true
  }
}
```

配置项说明：

| 配置项 | 作用 |
|--------|------|
| `ConnectionStrings:Default` | 与 Provider 共用的 `nexusauth` 数据库连接字符串。 |
| `Auth:Authority` | 浏览器和公开 OIDC 地址；必须与 Provider 的 `Jwt:Issuer` 完全一致。 |
| `Auth:BackchannelAuthority` | API 到 Provider 的内部地址。容器内访问时可设为 `http://sso:8080`；留空时使用 `Authority`。 |
| `Auth:ClientId` | 已由 seed 登记的 OAuth client ID。 |
| `Auth:ClientSecret` | 该 client 的共享密钥。API 启动时会创建或同步它。 |
| `Auth:RedirectUri` | Provider 授权回调，必须登记为 `http://localhost:5051/signin-oidc` 或对应生产 HTTPS 地址。 |
| `Auth:PostLogoutRedirectUri` | Provider 登出回调和 Dashboard 公共地址，必须登记且通常以 `/` 结尾。 |
| `Auth:Scope` | 内置 client 请求的 scope；默认包含 `offline_access` 以支持 refresh token。 |
| `Auth:Audience` | Bearer access token 的 audience。应与 API resource 的 audience 一致；内置 seed 使用 `workbench`。 |
| `Auth:RequireHttpsMetadata` | 是否要求 HTTPS Discovery 元数据。开发 HTTP 为 `false`，生产 HTTPS 应为 `true`。 |
| `Auth:SignOutProvider` | 是否在 Workbench 登出时生成 Provider 的 RP-Initiated Logout 地址。 |

### 环境变量

Workbench API 的 `Program.cs` 显式支持以下单下划线变量：

| 环境变量 | 配置项 |
|----------|--------|
| `NEXUSAUTH_WORKBENCH_CONNECTION_STRINGS_DEFAULT` | `ConnectionStrings:Default` |
| `NEXUSAUTH_WORKBENCH_AUTH_AUTHORITY` | `Auth:Authority` |
| `NEXUSAUTH_WORKBENCH_AUTH_BACKCHANNEL_AUTHORITY` | `Auth:BackchannelAuthority` |
| `NEXUSAUTH_WORKBENCH_AUTH_CLIENT_ID` | `Auth:ClientId` |
| `NEXUSAUTH_WORKBENCH_AUTH_CLIENT_SECRET` | `Auth:ClientSecret` |
| `NEXUSAUTH_WORKBENCH_AUTH_REDIRECT_URI` | `Auth:RedirectUri` |
| `NEXUSAUTH_WORKBENCH_AUTH_POST_LOGOUT_REDIRECT_URI` | `Auth:PostLogoutRedirectUri` |
| `NEXUSAUTH_WORKBENCH_AUTH_SCOPE` | `Auth:Scope` |
| `NEXUSAUTH_WORKBENCH_AUTH_AUDIENCE` | `Auth:Audience` |
| `NEXUSAUTH_WORKBENCH_AUTH_REQUIRE_HTTPS_METADATA` | `Auth:RequireHttpsMetadata` |
| `NEXUSAUTH_WORKBENCH_AUTH_SIGN_OUT_PROVIDER` | `Auth:SignOutProvider` |

Compose 使用容器内的 `NEXUSAUTH_WORKBENCH_AUTH_BACKCHANNEL_AUTHORITY=http://sso:8080`，而对浏览器公开的 `Authority` 仍是 `http://localhost:5100`。部署到域名后，`Authority`、Provider `NEXUSAUTH_JWT_ISSUER`、回调地址和 seed 中的 URI 必须一起更新。

## 3. 登录流程

内置实现位于 `admin/src/NexusAuth.Workbench.Api/Controllers/AuthController.cs`，流程如下：

1. Dashboard 调用 `GET /api/auth/login`。
2. API 从 Discovery 获取授权端点，生成随机 `state`、`nonce` 和 S256 PKCE 码对，并将流程状态暂存在 `IFlowStateStore`。
3. API 返回 `authorizeUrl`，Dashboard 将浏览器重定向到 Provider 的 `/connect/authorize`。
4. Provider 登录成功后回调 API 的 `GET /signin-oidc?code=...&state=...`。
5. API 在服务端使用 `client_secret_basic` 和 `code_verifier` 调用 `/connect/token`，读取 ID token 并创建紧凑的用户身份。
6. API 将 access token、refresh token、ID token 和过期时间写入受保护的 `.NexusAuth.Workbench` Cookie，然后重定向到 Dashboard 的 `/auth/callback`。
7. Dashboard 调用 `GET /api/auth/me` 确认会话，再访问管理接口。

Provider 登录页的“几天内免登录”是 Provider SSO Cookie 的策略；Workbench 自己的 BFF Cookie 默认是 24 小时并允许滑动续期，两者不是同一个会话。Workbench API 会在 access token 临近过期时自动轮换 refresh token，并同时续期自己的 BFF Cookie。

### 流程状态和密钥持久化

`NexusAuth.Extension` 当前默认使用 `InMemoryFlowStateStore`。单实例开发环境可以直接使用；多实例部署或 API 重启会丢失进行中的登录流程，需要在扩展层替换为共享的状态存储，并为 ASP.NET Data Protection 配置持久化且所有实例共享的密钥。

## 4. API 端点

### 认证接口

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/auth/config` | GET | 返回 Dashboard 所需的公开 OIDC 地址、client ID 和回调地址。 |
| `/api/auth/login` | GET | 创建 PKCE 流程并返回 `authorizeUrl`。 |
| `/signin-oidc` | GET | Provider 回调并建立 BFF Cookie。 |
| `/api/auth/me` | GET | 返回当前用户；未登录返回 HTTP 401。 |
| `/api/auth/logout` | POST | 清理 BFF Cookie，并按配置返回 Provider 登出地址。 |

### 管理接口

除认证接口外，管理 API 默认需要授权，并同时支持 BFF Cookie 和 Bearer access token：

| 路径 | 功能 |
|------|------|
| `/api/clients` | OAuth client 查询、创建、更新、删除和凭据生成/重置 |
| `/api/api-resources` | API resource 查询、创建、更新、状态和删除 |
| `/api/users` | 用户查询、资料/状态修改和密码重置 |
| `/api/scim-credentials` | SCIM service principal 凭据管理 |
| `/api/login-audits` | 登录审计记录查询 |
| `/api/client-metadata` | 客户端元数据和可选项 |
| `/swagger` | 开发环境 API 文档 |

管理接口返回的业务响应由 API result 包装；内置 Dashboard 的 `src/api/request.ts` 会统一解包成功结果并处理 401。自定义前端应保持 `withCredentials: true`，否则不会发送 BFF Cookie。

## 5. 在其他 .NET 应用中复用扩展

`NexusAuth.Extension` 是仓库内项目，目前不是已发布的 NuGet 包。项目引用示例：

```xml
<ProjectReference Include="../NexusAuth.Extension/NexusAuth.Extension.csproj" />
```

注册扩展：

```csharp
services.AddNexusAuth(options =>
{
    options.Authority = configuration["Auth:Authority"]!;
    options.BackchannelAuthority = configuration["Auth:BackchannelAuthority"];
    options.ClientId = configuration["Auth:ClientId"]!;
    options.ClientSecret = configuration["Auth:ClientSecret"];
    options.RedirectUri = configuration["Auth:RedirectUri"]!;
    options.PostLogoutRedirectUri = configuration["Auth:PostLogoutRedirectUri"]!;
    options.Scope = configuration["Auth:Scope"]!;
    options.SignOutProvider = true;
});
```

`AddNexusAuth` 只注册 OIDC 服务、PKCE 流程状态存储和 HTTP client，不会自动添加 `/api/auth/login`、`/signin-oidc`、Cookie 认证或前端路由。自定义应用应参考内置 `AuthController`、`WorkbenchApiModule` 和 Dashboard 的 API 调用实现完整回调流程，并在服务端保存令牌。

## 6. 常见问题

- API 启动提示 OAuth client 不存在：先执行 `seed.sql`，并确认连接字符串的 `Search Path` 为 `nexusauth`。
- 登录回调后又回到登录页：检查 `Authority`、`BackchannelAuthority`、`RedirectUri`、`PostLogoutRedirectUri` 和 Provider `Issuer` 是否分别对应浏览器与容器网络。
- 管理 API 返回 401：确认 Dashboard 请求带有 `withCredentials: true`，并检查 `Auth:Audience` 是否为 `workbench`；Compose 已设置该值。
- 令牌刷新失败：确认客户端允许 `refresh_token` grant 和 `offline_access` scope，且 API 配置的 client secret 与 Provider 数据库中的当前凭据一致。
- 多实例登录偶发 `invalid_callback`：默认内存流程状态无法跨实例共享，需实现分布式 `IFlowStateStore` 并共享 Data Protection keys。

下一步阅读 [启动 Dashboard](./07-对接NexusAuth.Workbench.Dashboard.md) 或 [高级配置](./08-高级配置.md)。
