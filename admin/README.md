# NexusAuth Admin

`NexusAuth.Admin.sln` 是 NexusAuth Workbench 的开发解决方案。Workbench 与 Provider 独立部署：Workbench API 负责 OIDC BFF 和管理接口，Dashboard 负责浏览器界面；共享领域、应用、持久化和基础设施项目仍位于仓库根目录的 `src/` 下。

## 组件与地址

| 组件 | 项目路径 | 本地地址 | 作用 |
|---|---|---|---|
| Provider | `src/NexusAuth.Host` | `http://localhost:5100` | 用户登录、授权、令牌和 OIDC 端点 |
| Workbench API | `admin/src/NexusAuth.Workbench.Api` | `http://localhost:5051` | OIDC BFF、管理 API、Swagger |
| Dashboard | `admin/src/NexusAuth.Workbench.Dashboard` | `http://localhost:5273` | React 管理界面 |
| Extension | `admin/src/NexusAuth.Extension` | - | Workbench 的 OIDC、PKCE 和 token 客户端实现 |

`admin/src/NexusAuth.Workbench.Api` 和 Dashboard 可以分别部署。生产环境应让浏览器只访问 Dashboard 域名，由 Nginx 或其他反向代理把 `/api` 转发到 Workbench API；客户端密钥、access token 和 refresh token 不进入浏览器。

## 运行前提

- .NET 10 SDK；
- Node.js 和 npm（仅本地运行 Dashboard 需要）；
- PostgreSQL 16，或仓库根目录的 Docker Compose；
- 已执行当前版本的 `production-init.sql`；
- 已登记 `nexusauth.workbench` 客户端和 `workbench` API resource。

Workbench seed 位于 `admin/src/NexusAuth.Workbench.Api/seed.sql`。它只登记客户端元数据和资源，不写入共享密钥明文；Workbench API 启动时从配置读取密钥并同步 BCrypt 哈希。

## 本地启动

从仓库根目录依次启动 Provider 和 Workbench API：

```bash
dotnet run --project src/NexusAuth.Host
dotnet run --project admin/src/NexusAuth.Workbench.Api
```

再安装并启动 Dashboard：

```bash
npm --prefix admin/src/NexusAuth.Workbench.Dashboard install
npm --prefix admin/src/NexusAuth.Workbench.Dashboard run dev
```

打开 `http://localhost:5273`。Dashboard 的 Vite 开发代理把 `/api` 转发到 `http://localhost:5051`，因此浏览器与 BFF 使用同源 Cookie。Workbench API 的 Swagger 地址是 `http://localhost:5051/swagger`。

也可以直接从仓库根目录运行：

```bash
docker compose up --build
```

Compose 会启动 PostgreSQL、Provider、Workbench API 和 Nginx Dashboard；Demo 项目不包含在 Compose 服务中。数据库脚本只会在新的 PostgreSQL 数据卷首次初始化时执行。

## Workbench API 配置

`admin/src/NexusAuth.Workbench.Api/appsettings.json` 的核心配置如下。生产环境应通过 Secret 或环境变量提供 `ClientSecret`：

```json
{
  "Auth": {
    "Authority": "https://sso.example.com",
    "BackchannelAuthority": "http://sso:8080",
    "ClientId": "nexusauth.workbench",
    "ClientSecret": "从 Secret 注入",
    "RedirectUri": "https://api.example.com/signin-oidc",
    "PostLogoutRedirectUri": "https://console.example.com/",
    "Scope": "openid profile workbench offline_access",
    "Audience": "workbench",
    "RequireHttpsMetadata": true,
    "SignOutProvider": true
  }
}
```

| 配置 | 作用 |
|---|---|
| `Authority` | Provider 的公开 Issuer，用于浏览器跳转和 JWT issuer 校验 |
| `BackchannelAuthority` | API 从网络内部访问 Discovery、token、introspection 等端点的地址；Compose 内为 `http://sso:8080` |
| `ClientId` | Workbench OAuth 客户端，固定为 `nexusauth.workbench`（除非重新登记） |
| `ClientSecret` | Workbench API 兑换授权码、刷新和 introspection 所需的机密；必须与 Provider 数据库中的客户端密钥一致 |
| `RedirectUri` | Provider 登记的 OIDC 回调，必须逐字符匹配 |
| `PostLogoutRedirectUri` | Provider 登记的登出回调，必须逐字符匹配 |
| `Scope` | Workbench 申请的 scope，通常包含 `openid profile workbench offline_access` |
| `Audience` | Bearer access token 的 audience；应与 API resource 的 `audience` 一致 |
| `RequireHttpsMetadata` | 是否强制 Discovery 元数据使用 HTTPS；生产应为 `true` |
| `SignOutProvider` | 是否在 Workbench 登出后继续跳 Provider 完成全局登出 |

上面的 `Audience=workbench` 与当前 Workbench seed 中 API resource 的 `audience` 对齐。如果自定义 resource 或本地配置仍使用 `workbench-api`，必须让 seed、签发 token 和 API 验证三者使用同一个值，否则 Bearer 请求会被拒绝。

### 环境变量

Workbench API 使用带 `NEXUSAUTH_WORKBENCH_` 前缀的单下划线变量覆盖配置，不要只照搬标准 .NET 的双下划线名称：

```bash
NEXUSAUTH_WORKBENCH_CONNECTION_STRINGS_DEFAULT="Host=db;Port=5432;Database=nexusauth;Username=nexusauth;Password=REPLACE_WITH_A_SECRET;Search Path=nexusauth"
NEXUSAUTH_WORKBENCH_AUTH_AUTHORITY=https://sso.example.com
NEXUSAUTH_WORKBENCH_AUTH_BACKCHANNEL_AUTHORITY=http://sso:8080
NEXUSAUTH_WORKBENCH_AUTH_CLIENT_ID=nexusauth.workbench
NEXUSAUTH_WORKBENCH_AUTH_CLIENT_SECRET=REPLACE_WITH_A_LONG_RANDOM_SECRET
NEXUSAUTH_WORKBENCH_AUTH_REDIRECT_URI=https://api.example.com/signin-oidc
NEXUSAUTH_WORKBENCH_AUTH_POST_LOGOUT_REDIRECT_URI=https://console.example.com/
NEXUSAUTH_WORKBENCH_AUTH_SCOPE="openid profile workbench offline_access"
NEXUSAUTH_WORKBENCH_AUTH_AUDIENCE=workbench
NEXUSAUTH_WORKBENCH_AUTH_REQUIRE_HTTPS_METADATA=true
NEXUSAUTH_WORKBENCH_AUTH_SIGN_OUT_PROVIDER=true
```

Compose 中的 `WORKBENCH_CLIENT_SECRET` 同时用于数据库初始化流程和 API 的 `Auth:ClientSecret`。变更密钥时，先更新 Secret，再按部署策略重新初始化/同步客户端并滚动重启 API；不要把真实密钥提交到 `appsettings.json`、SQL 或前端环境变量。

### 数据库 seed

`admin/src/NexusAuth.Workbench.Api/seed.sql` 会登记：

- `nexusauth.workbench` OAuth 客户端；
- `openid`、`profile`、`workbench` API resources；
- 客户端与 API resource 的映射；
- authorization code + PKCE 和 refresh token grant 的允许范围。

它不会写入客户端共享密钥。`WorkbenchClientCredentialHostedService` 在 API 启动时读取 `Auth:ClientSecret`，找不到客户端、密钥为空或同步失败会阻止 API 正常启动。

## 登录、会话与登出

1. Dashboard 请求 `GET /api/auth/me`；未登录时调用 `GET /api/auth/login`。
2. API 生成 `state`、`nonce` 和 S256 PKCE 参数，浏览器跳转 Provider 的 `/connect/authorize`。
3. Provider 回调 API 的 `GET /signin-oidc`；API 使用 `client_secret_basic` 在服务端兑换授权码，解析 ID Token 并签发 `.NexusAuth.Workbench` Cookie。
4. API 重定向 Dashboard 的 `/auth/callback`，Dashboard 再读取 `/api/auth/me`。
5. 普通请求不带 Bearer 时使用 Cookie；带 `Authorization: Bearer ...` 时，policy scheme 转交 JWT bearer 验证。
6. `POST /api/auth/logout` 清除 Workbench Cookie；`SignOutProvider=true` 且存在 ID Token 时，前端继续跳 Provider 的 `/connect/endsession`。

Workbench Cookie 名为 `.NexusAuth.Workbench`，HttpOnly、SameSite=Lax，默认是独立的 24 小时 session，并开启滑动续期。它与 Provider 登录页的“几天内免登录”不是同一层配置。

每次 Cookie 验证都会检查 token 状态：

- access token 距离过期超过 1 分钟时，API 通过 introspection 验证 token 仍 active、用户和客户端归属正确；
- 距离过期不超过 1 分钟时，API 使用 refresh token 自动刷新，并保存轮换后的新 refresh token、access token 和 ID Token；
- refresh 成功后将 Cookie session 和 token 续到新的 24 小时窗口；
- token 缺失、用户不匹配、introspection 失败或 refresh 失败时拒绝 Principal 并清除 Cookie，前端需要重新登录。

多副本部署必须共享并持久化 API 的 ASP.NET Core Data Protection key ring，否则切换实例后 Cookie 无法解密。

## 管理接口

管理接口需要 Workbench Cookie 或有效的 Workbench Bearer access token；认证入口本身允许匿名访问。当前控制器提供：

| 路由 | 能力 |
|---|---|
| `/api/clients` | OAuth 客户端查询、创建、更新、凭据生成/重置和删除 |
| `/api/users` | 用户查询、资料更新、启停和密码重置 |
| `/api/api-resources` | API resource 查询、创建、更新、启停和删除 |
| `/api/client-metadata` | 客户端元数据查询 |
| `/api/login-audits` | 登录审计查询 |
| `/api/scim-credentials` | SCIM 凭证查询、创建、更新和撤销 |
| `/api/auth/config` | 返回可公开的 Provider、client ID 和回调配置 |
| `/api/auth/login`、`/signin-oidc`、`/api/auth/me`、`/api/auth/logout` | BFF 登录生命周期 |

普通管理控制器使用统一 API 结果包装；`/api/auth/*` 和 `/signin-oidc` 是认证流程端点，按 HTTP 状态和认证响应处理。Swagger 是接口细节的最终来源。

## 构建与部署

从仓库根目录构建：

```bash
dotnet build admin/NexusAuth.Admin.sln
npm --prefix admin/src/NexusAuth.Workbench.Dashboard run build
npm --prefix admin/src/NexusAuth.Workbench.Dashboard run lint
```

Dashboard 的 `npm run build` 会先执行 `tsc -b`，再执行 Vite build；没有单独的 `npm run typecheck` 脚本。

Compose 的 Dashboard 镜像从仓库根目录构建，使用 Node 22 生成静态资源，再由 Nginx 1.27 提供服务。宿主机 `5273:80`，Nginx 将 `/api` 代理到 `admin-api:8080`，其余路径回退到 `index.html`。

生产部署至少应确认：

- Provider 中登记的 `RedirectUri` 和 `PostLogoutRedirectUri` 与 API 配置逐字符一致；
- `Authority` 使用公网 HTTPS，`BackchannelAuthority` 只解决容器内部寻址，不改变 token 的 `iss`；
- `Audience` 与 Workbench API resource 的 `audience` 一致；
- Dashboard/API 反向代理保留 Cookie、`X-Forwarded-Proto` 和 `X-Forwarded-Host`；
- 多副本 API 共享 Data Protection key ring，数据库使用持久卷并有备份；
- 不在生产执行 `demo/seed.sql`，不使用仓库中的开发密钥。

## 相关文档

- [Workbench API 接入说明](../document/06-对接NexusAuth.Workbench.md)
- [Dashboard 使用说明](./src/NexusAuth.Workbench.Dashboard/README.md)
- [高级配置](../document/08-高级配置.md)
- [常见问题](../document/09-常见问题.md)
- [OAuth/OIDC 协议设计](../document/11-OAuth-OIDC协议设计.md)
- [完整使用手册](../document/12-使用手册.md)
