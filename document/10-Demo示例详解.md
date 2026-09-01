# Demo 示例详解

本文说明仓库 `demo/` 下各示例的用途、初始化方式、启动命令和令牌行为。所有示例都面向本地开发；其中的用户、客户端、私钥和共享密钥都不能直接用于生产。

## 1. 运行前准备

从仓库根目录执行命令。先准备：

- .NET 10 SDK；
- Node.js 和 npm（仅 Web Demo 需要）；
- PostgreSQL，或仓库根目录的 Docker Compose；
- 已初始化的 NexusAuth 数据库。

最省事的本地服务启动方式是：

```bash
docker compose up --build
```

Compose 默认启动 Provider、Workbench API、Dashboard 和 PostgreSQL，但不会启动 `demo/` 下的 BFF 或前端。Demo 需要另外启动，并使用本机的 Provider 地址 `http://localhost:5100`。

## 2. 初始化 Demo 数据

`production-init.sql` 只创建空库的最终 schema；`admin/src/NexusAuth.Workbench.Api/seed.sql` 登记 Workbench 客户端和资源；`demo/seed.sql` 才包含 Demo 客户端、API resources 和测试用户。

### 使用本地 PostgreSQL

确认数据库和角色已由部署/DBA 创建后，从仓库根目录执行：

```bash
psql -U nexusauth -d nexusauth -v ON_ERROR_STOP=1 -f production-init.sql
psql -U nexusauth -d nexusauth -v ON_ERROR_STOP=1 -f demo/seed.sql
```

Workbench seed 需要在同一个 psql 会话中设置 schema：

```bash
psql -U nexusauth -d nexusauth -v ON_ERROR_STOP=1 <<'SQL'
SET search_path TO nexusauth;
\i admin/src/NexusAuth.Workbench.Api/seed.sql
SQL
```

如果是全新数据库，先执行 `production-init.sql`；不要把它重复执行到已有数据的数据库上。脚本不会创建 PostgreSQL 数据库或角色。

### 使用 Docker Compose 数据库

Compose 的新数据卷会自动执行 `production-init.sql` 和 Workbench seed，但不会执行 `demo/seed.sql`。服务启动后可从仓库根目录手动导入 Demo seed：

```bash
docker compose exec -T db psql -U nexusauth -d nexusauth -v ON_ERROR_STOP=1 < demo/seed.sql
```

如果修改了 seed，旧数据卷不会自动重放脚本。仅在本地数据可以删除时，才执行 `docker compose down -v` 后重新启动；该命令会删除数据库、签名密钥和 Data Protection 数据卷。

### 测试账号

`demo/seed.sql` 创建以下本地账号，密码均为 `Pass@123`：

| 用户名 | 用途 |
|---|---|
| `alice` | 普通登录演示 |
| `bob` | 普通登录演示 |
| `admin` | 系统管理员演示 |

## 3. Demo 总览

| 示例 | 项目 | 客户端 | 认证方式 | 默认地址/用途 |
|---|---|---|---|---|
| Web + BFF | `Demo.Web` + `Demo.Bff` | `demo-bff` | `private_key_jwt` | 前端 `5200`，BFF `5201` |
| Web + BFF Secret | `Demo.Web.ClientSecret` + `Demo.Bff.ClientSecret` | `demo-bff-secret` | `client_secret_basic` | 前端 `5300`，BFF `5301` |
| Client Credentials | `Demo.ClientCredentials` | `demo-cc` | `private_key_jwt` | 调用 BFF Bearer API |
| Device Code | `Demo.DeviceCode` | `demo-device` | `private_key_jwt` | 受限输入设备授权 |
| Refresh Token | `Demo.RefreshToken` | 复用 `demo-device` | `private_key_jwt` | 单独验证刷新和轮换 |

`demo-bff` 和 `demo-bff-secret` 的授权码客户端都要求 PKCE；两个控制台客户端不需要授权码 PKCE，因为它们不走浏览器授权码流程。

## 4. Web + BFF：private_key_jwt

### 启动

先启动 BFF：

```bash
dotnet run --project demo/src/Demo.Bff
```

再启动前端：

```bash
npm --prefix demo/src/Demo.Web install
npm --prefix demo/src/Demo.Web run dev
```

访问 <http://localhost:5200>。前端的 Vite 配置把 `/api` 代理到 `http://localhost:5201`。

### 登录流程

1. 前端调用 BFF 的 `GET /api/auth/login`。
2. BFF 从 Provider Discovery 读取授权端点，生成 `state`、`nonce` 和 S256 PKCE 参数。
3. 浏览器跳转 Provider 的 `/connect/authorize`，使用 `alice / Pass@123` 登录并同意授权。
4. Provider 回调 BFF 的 `GET /signin-oidc`。
5. BFF 在服务端用 `private_key_jwt` 兑换授权码，校验 ID Token 的签名、issuer、audience、有效期和 nonce，再调用 UserInfo。
6. BFF 将令牌和用户信息放入受保护的 Cookie session，重定向前端 `/auth/callback`。
7. 前端调用 `GET /api/auth/me`，随后可调用 `GET /api/profile`。

BFF 的私钥位于 `demo/src/Demo.Bff/keys/demo-bff-private.pem`，Provider 使用 `demo/seed.sql` 中 `demo-bff` 的内嵌 JWKS 验签。该私钥只是仓库开发材料，生产必须替换为受管密钥。

### BFF 接口

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/config` | 返回可公开的 Provider/client 配置 |
| GET | `/api/auth/login` | 创建 OIDC 授权请求 |
| GET | `/signin-oidc` | 服务端 OIDC 回调和授权码兑换 |
| GET | `/api/auth/me` | 返回当前 BFF session 用户 |
| POST | `/api/auth/logout` | 撤销令牌、清 Cookie，并返回 Provider 登出地址 |
| GET | `/api/profile` | 需要 BFF Cookie 的用户 API |
| GET | `/api/m2m/profile` | 需要 Bearer access token 的 API |

## 5. Web + BFF：client_secret_basic

### 启动

```bash
dotnet run --project demo/src/Demo.Bff.ClientSecret
npm --prefix demo/src/Demo.Web.ClientSecret install
npm --prefix demo/src/Demo.Web.ClientSecret run dev
```

访问 <http://localhost:5300>。前端的 `/api` 代理指向 `http://localhost:5301`。

该项目通过 `Demo.Bff.ClientSecret.csproj` 复用 `Demo.Bff` 的源码，只替换 `appsettings.json`：客户端是 `demo-bff-secret`，认证方式是 `client_secret_basic`，回调地址是 `http://localhost:5301/signin-oidc`。本地共享密钥写在该 Demo 的 appsettings 中，仅供测试；生产必须从 Secret 注入，并与 Provider 中的客户端密钥一致。

流程和 API 与上一节相同，但 BFF 兑换 token、撤销 token 时通过 HTTP Basic 认证。两个 Web Demo 可以同时运行，因为端口、Cookie 名、客户端 ID 和回调地址均不同。

## 6. Client Credentials

`Demo.ClientCredentials` 是无用户的机器到机器示例：

```bash
dotnet run --project demo/src/Demo.ClientCredentials
```

默认行为：

1. 从 `http://localhost:5100/.well-known/openid-configuration` 读取 `token_endpoint`；
2. 使用 `demo-cc` 私钥生成 RS256 `client_assertion`；
3. 以 `grant_type=client_credentials` 和 `scope=demo-bff-api` 请求 access token；
4. 使用 `Authorization: Bearer <access_token>` 调用 `http://localhost:5201/api/m2m/profile`。

此流程不代表用户身份，不签发 ID Token 或 refresh token。若要调用第二套 BFF，可通过 `DEMO_BFF_API` 覆盖 API 地址，并确保目标 API 接受 `demo-bff-api` audience。

常用环境变量：

| 变量 | 默认值 |
|---|---|
| `NEXUSAUTH_AUTHORITY` | `http://localhost:5100` |
| `NEXUSAUTH_CLIENT_ID` | `demo-cc` |
| `NEXUSAUTH_CLIENT_PRIVATE_KEY_PATH` | `keys/demo-client-private.pem` |
| `NEXUSAUTH_CLIENT_KEY_ID` | `demo-cc-key-1` |
| `NEXUSAUTH_SCOPE` | `demo-bff-api` |
| `DEMO_BFF_API` | `http://localhost:5201/api/m2m/profile` |

## 7. Device Code

启动：

```bash
dotnet run --project demo/src/Demo.DeviceCode
```

程序会：

1. 以 `demo-device` 和 `private_key_jwt` 调用 `POST /connect/deviceauthorization`；
2. 输出 `user_code`、`verification_uri` 和 `verification_uri_complete`；
3. 在浏览器打开验证地址，按 Provider 页面要求登录并批准；
4. 按服务端返回的 `interval` 轮询 `POST /connect/token`；
5. 收到 access token 后调用 Demo BFF 的 `/api/m2m/profile`；
6. 自动用返回的 refresh token 测试一次 refresh token grant，再用新 access token 调用一次 API；
7. 打印初始 token 响应以及自动 refresh 的响应。由于初始 refresh token 已在第 6 步被消费，手工运行 `Demo.RefreshToken` 时必须从自动 refresh 响应中的 `refresh_token` 字段复制新值；当前示例末尾再次打印的初始值已经不能重用。

轮询中看到 `authorization_pending` 时继续等待；看到 `slow_down` 时增加间隔；看到 `expired_token`、`access_denied` 或 `invalid_grant` 时需要重新发起设备授权。默认 device code 有效期为 15 分钟。

环境变量与 `Demo.ClientCredentials` 相同，默认 client ID 为 `demo-device`，默认 scope 为 `openid profile email phone offline_access demo-bff-api`，默认 API 为 `http://localhost:5201/api/m2m/profile`。

## 8. Refresh Token

先运行 `Demo.DeviceCode` 完成一次授权，并从它打印的自动 refresh 响应中复制新 `refresh_token`，再启动：

```bash
dotnet run --project demo/src/Demo.RefreshToken
```

程序优先读取 `NEXUSAUTH_REFRESH_TOKEN`；未设置时会提示手工输入。它使用 `demo-device` 的 `private_key_jwt` 调用 `POST /connect/token`，发送 `grant_type=refresh_token`，并打印响应。

refresh token 是一次性轮换材料：成功刷新后，响应中的新 token 才能继续使用，旧 token 应立即丢弃。不要把 token 放进 shell 历史、源代码、Issue 或普通日志中。

## 9. 生命周期与自动刷新

Provider 当前默认值：

| 配置 | 默认值 | 作用 |
|---|---:|---|
| `Jwt:AccessTokenLifetimeMinutes` | 60 分钟 | access token 有效期 |
| `Jwt:RefreshTokenLifetimeMinutes` | 43200 分钟 | refresh token 绝对有效期，即 30 天 |
| `Jwt:DeviceCodeLifetimeMinutes` | 15 分钟 | device code 有效期 |

两个 Web BFF 的开发配置都将 `Session:CookieLifetimeMinutes` 设为 3 分钟，Cookie 开启滑动过期。每次 BFF Cookie 验证时，如果 access token 即将过期且存在 refresh token，BFF 会在服务端自动刷新、更新受保护 session，并继续执行业务请求；刷新失败则返回未认证状态。

Workbench 的 Cookie session 是独立的 24 小时窗口，不使用 Demo 的 3 分钟配置。

Provider 登录页的“几天内免登录”是另一层会话设置：

```bash
NEXUSAUTH_LOGIN_FLOW_REMEMBER_ME_LIFETIME_DAYS=3
```

默认 3 天，允许 1–30 天。用户勾选后，Provider 持久 Cookie 和数据库 SSO session 固定在该期限后过期，不因访问而滑动续期。它不会延长 Demo BFF 自身 3 分钟 Cookie；但当 BFF Cookie 失效后，Provider 的 SSO 会话仍可能让下一次授权无需重新输入密码。

## 10. 安全边界

- Demo 私钥、`demo-bff-secret` 共享密钥和 `alice / Pass@123` 只用于本地测试。
- 浏览器只调用 BFF；access token、refresh token、ID Token 和客户端密钥由 BFF 服务端保管。
- `private_key_jwt` 使用 RS256，Provider 验证客户端登记的内嵌 JWKS；轮换密钥时要同步更新客户端元数据和服务端私钥。
- Web Demo 的回调地址必须与 seed 中登记值完全一致，不能用通配符。
- refresh token 轮换后不要并发重用旧 token；登出时同时撤销 access token 和 refresh token。
- 生产环境使用 HTTPS、受管证书和 Secret 存储，不要直接复制仓库中的 `keys/` 文件。

## 11. 常见故障

| 现象 | 检查项 |
|---|---|
| `invalid_client` | 客户端 ID、认证方式、私钥/JWKS 或共享密钥是否匹配；认证失败是 401。 |
| `invalid_request` | 回调地址是否精确匹配；参数是否使用 form-urlencoded。 |
| `invalid_grant` | 授权码或 refresh token 是否已使用、过期，PKCE verifier 是否对应本次授权。 |
| 浏览器跳回后显示 `invalid_callback` | BFF 是否重启导致内存中的 state 丢失；回调端口是否正确。 |
| BFF 业务接口 401 | Cookie 是否过期，refresh token 是否已轮换/失效，BFF 是否能访问 Provider。 |
| Device Code 一直 pending | 是否已在 `verification_uri_complete` 完成登录和批准；轮询是否遵循 interval。 |
| 数据库表不存在 | 是否先执行 `production-init.sql`，以及 SQL 的 `search_path` 是否为 `nexusauth`。 |
| 登录请求 429 | 按响应的 `Retry-After: 60` 等待，不要提高轮询或重试频率。 |

更多 Provider、数据库和 Docker 排障见 [常见问题](./09-常见问题.md)，Workbench 配置见 [对接 NexusAuth.Workbench](./06-对接NexusAuth.Workbench.md)。
