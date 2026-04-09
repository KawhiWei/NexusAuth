# NexusAuth

`Nexus` 意为“连接点”，隐喻统一认证中心作为多个系统之间的连接枢纽。
<img width="2586" height="1487" alt="image" src="https://github.com/user-attachments/assets/8995624b-b769-401b-bae5-9c4a8a998c33" />

NexusAuth 是一个基于 ASP.NET Core 的 OAuth 2.0 / OpenID Connect 认证授权服务示例工程，当前项目已经具备以下能力：

- OAuth 2.0 授权模式
  - `authorization_code + PKCE`
  - `client_credentials`
  - `device_code`
  - `refresh_token`
- OpenID Connect 能力
  - `/.well-known/openid-configuration`
  - `/.well-known/jwks.json`
  - `userinfo`
  - `id_token`
  - `end_session`
- 客户端认证方式
  - `client_secret_basic`
  - `client_secret_post`
  - `private_key_jwt`
- 安全增强
  - `authorization_code` 强制 `S256 PKCE`
  - `private_key_jwt` 强制 `RS256`
  - `private_key_jwt` 支持 `jti` 防重放
  - refresh token 与 `client_id` 绑定
  - revocation / introspection 客户端边界检查

当前仓库同时包含完整的 demo 工程，覆盖浏览器登录、机器到机器调用、设备授权以及 refresh token 续签测试。

## 仓库结构

```text
NexusAuth/
├── src/                             # NexusAuth 核心服务
├── demo/                            # 所有 demo 项目与 SQL
│   ├── schema.sql                   # demo 用完整建库脚本（可重建）
│   ├── seed.sql                     # demo 用种子数据脚本
│   └── src/
│       ├── Demo.Bff                # private_key_jwt 版 BFF
│       ├── Demo.Web                # private_key_jwt 版前端
│       ├── Demo.Bff.ClientSecret   # client_secret_basic 版 BFF
│       ├── Demo.Web.ClientSecret   # client_secret_basic 版前端
│       ├── Demo.ClientCredentials  # client_credentials demo
│       ├── Demo.DeviceCode         # device_code demo（自动 refresh 测试）
│       └── Demo.RefreshToken       # 独立 refresh_token demo
├── production-init.sql              # 生产初始化结构脚本（不含 demo 数据）
├── auth-flow.md                     # 认证授权流程图索引
├── auth-sequence-authorization-code.mmd
├── auth-sequence-client-credentials.mmd
├── auth-sequence-device-code.mmd
└── auth-sequence-refresh-token.mmd
```

## 当前支持的 Demo 场景

### 1. `private_key_jwt` 版 Web 登录

- 前端：`demo/src/Demo.Web`
- BFF：`demo/src/Demo.Bff`
- `client_id`：`demo-bff`
- 客户端认证方式：`private_key_jwt`
- 端口：
  - Frontend：`5200`
  - BFF：`5201`

### 2. `client_secret_basic` 版 Web 登录

- 前端：`demo/src/Demo.Web.ClientSecret`
- BFF：`demo/src/Demo.Bff.ClientSecret`
- `client_id`：`demo-bff-secret`
- 客户端认证方式：`client_secret_basic`
- 端口：
  - Frontend：`5300`
  - BFF：`5301`

### 3. `client_credentials`

- 项目：`demo/src/Demo.ClientCredentials`
- `client_id`：`demo-cc`
- 客户端认证方式：`private_key_jwt`

### 4. `device_code`

- 项目：`demo/src/Demo.DeviceCode`
- `client_id`：`demo-device`
- 客户端认证方式：`private_key_jwt`
- 特点：已自动串联 refresh token 流程测试

### 5. `refresh_token`

- 项目：`demo/src/Demo.RefreshToken`
- `client_id`：复用 `demo-device`
- 客户端认证方式：`private_key_jwt`

## 数据库初始化

### Demo 环境

如果你要完整运行 demo，请使用：

- `demo/schema.sql`
- `demo/seed.sql`

说明：

- `demo/schema.sql`：适合本地 / demo 环境，包含完整重建库逻辑
- `demo/seed.sql`：插入所有 demo client、demo 用户、demo API resource

测试账号：

- `alice / Pass@123`
- `bob / Pass@123`
- `admin / Pass@123`

### 生产 / 预发环境

不要直接使用 `demo/schema.sql` / `demo/seed.sql` 上生产。

推荐使用：

- `production-init.sql`

说明：

- 该脚本不会 `DROP DATABASE`
- 只创建表结构与索引
- 不写入 demo 用户、demo 客户端、demo 私钥
- 适合作为生产初始化脚本

## 当前数据库中的核心客户端

`demo/seed.sql` 当前包含这些 client：

| client_id | auth method | grant types | 说明 |
| --- | --- | --- | --- |
| `demo-bff` | `private_key_jwt` | `authorization_code`, `refresh_token` | Web + BFF（private_key_jwt） |
| `demo-bff-secret` | `client_secret_basic` | `authorization_code`, `refresh_token` | Web + BFF（client secret） |
| `demo-cc` | `private_key_jwt` | `client_credentials` | 机器到机器调用 |
| `demo-device` | `private_key_jwt` | `device_code`, `refresh_token` | 设备授权 + refresh token |
| `demo-cert` | `private_key_jwt` | `client_credentials` | 独立的 private_key_jwt 客户端示例 |

## 快速开始

### 1. 启动 NexusAuth.Host

```bash
dotnet run --project src/NexusAuth.Host/NexusAuth.Host.csproj
```

默认地址：

- `http://localhost:5100`

可检查：

- `http://localhost:5100/.well-known/openid-configuration`

### 2. 启动 private_key_jwt 版 BFF

```bash
dotnet run --project demo/src/Demo.Bff/Demo.Bff.csproj
```

默认地址：

- `http://localhost:5201`

可检查：

- `http://localhost:5201/api/config`

### 3. 启动 private_key_jwt 版前端

```bash
cd demo/src/Demo.Web
npm install
npm run dev
```

默认地址：

- `http://localhost:5200`

### 4. 启动 client_secret 版 BFF

```bash
dotnet run --project demo/src/Demo.Bff.ClientSecret/Demo.Bff.ClientSecret.csproj
```

默认地址：

- `http://localhost:5301`

可检查：

- `http://localhost:5301/api/config`

### 5. 启动 client_secret 版前端

```bash
cd demo/src/Demo.Web.ClientSecret
npm install
npm run dev
```

默认地址：

- `http://localhost:5300`

## 控制台 Demo 运行方式

### `client_credentials`

```bash
dotnet run --project demo/src/Demo.ClientCredentials/Demo.ClientCredentials.csproj
```

作用：

- 使用 `private_key_jwt` 换取 `access_token`
- 调用 `Demo.Bff` 的 `/api/m2m/profile`

### `device_code`

```bash
dotnet run --project demo/src/Demo.DeviceCode/Demo.DeviceCode.csproj
```

作用：

- 发起 device authorization
- 输出 `user_code` 与 `verification_uri`
- 轮询获取 `access_token` / `refresh_token`
- 自动调用一次 BFF API
- 自动测试一次 refresh token
- 再次调用 BFF API

### `refresh_token`

```bash
dotnet run --project demo/src/Demo.RefreshToken/Demo.RefreshToken.csproj
```

作用：

- 手工输入 `refresh_token`
- 单独验证 refresh token grant

## RefreshToken 自动续期说明

### Web + BFF 场景

当前 `Demo.Web + Demo.Bff` 已实现 BFF 服务端自动续期：

1. 用户登录成功后，BFF 将以下信息写入本地 Cookie Session：
   - `access_token`
   - `refresh_token`
   - `id_token`
   - `expires_in`
   - `issued_at`
   - `userinfo`
2. 每次浏览器带 Cookie 调用 BFF 时，Cookie 中间件会进入 `ValidatePrincipal`
3. BFF 自动检查 access token 是否即将过期
4. 若即将过期且存在 refresh token，则自动调用 `/connect/token`
5. 拿到新 token 后自动更新 Cookie Session
6. 前端无感知，业务请求继续执行

实现位置：

- `demo/src/Demo.Bff/Services/OidcSessionCookieEvents.cs`
- `demo/src/Demo.Bff/Services/OidcBffService.cs`

### DeviceCode 场景

`Demo.DeviceCode` 现在已经自动串联 refresh token：

1. device authorization 成功
2. 首次 access token 调 API
3. 自动 refresh token
4. 使用刷新后的 access token 再次调 API

## 当前测试用时间设置

为了方便本地测试 refresh token，当前默认配置已调短：

### Host

文件：`src/NexusAuth.Host/appsettings.json`

- `AccessTokenLifetimeMinutes = 1`
- `RefreshTokenLifetimeMinutes = 3`
- `DeviceCodeLifetimeMinutes = 15`

### BFF Cookie Session

文件：

- `demo/src/Demo.Bff/appsettings.json`
- `demo/src/Demo.Bff.ClientSecret/appsettings.json`

当前：

- `CookieLifetimeMinutes = 3`

说明：

- access token 1 分钟后过期
- refresh token 3 分钟后过期
- Cookie 会话 3 分钟有效
- 这样既能测到 refresh，又不会因为 Cookie 过短导致会话先失效

## Mermaid 时序图

这些 `.mmd` 文件都可以直接复制到：

- `https://mermaid.live/`

文件列表：

- `auth-sequence-authorization-code.mmd`
- `auth-sequence-client-credentials.mmd`
- `auth-sequence-device-code.mmd`
- `auth-sequence-refresh-token.mmd`

另外：

- `auth-flow.md` 用来做索引说明

## 生产化建议

当前项目已经具备完整演示能力，但如果要正式上线，建议继续补齐这些能力：

### 1. 私钥不要进仓库

对于 `private_key_jwt` 客户端：

- 不要把 `.pem` 提交到仓库
- 生产环境建议使用：
  - K8s Secret
  - External Secrets
  - Vault / KMS / HSM

### 2. 客户端独立 key / JWKS

当前 demo 已经做到了：

- 每个 demo client 使用独立 `kid`
- 各自独立 JWKS

生产环境建议继续支持：

- 多 key 并存
- 灰度轮换

### 3. 生产初始化脚本使用 `production-init.sql`

不要把 demo seed 直接带到生产。

### 4. BFF 多副本部署时注意共享会话能力

如果未来要多副本部署 BFF，除了 `private_key_jwt` 私钥共享外，还要考虑：

- Cookie / DataProtection key 的共享
- 统一的服务端会话策略

### 5. 日志与审计

建议对这些事件增加审计日志：

- token issuance
- refresh token 使用
- revocation
- private_key_jwt 验签失败
- `jti` 重放拒绝

## 常见问题

### 1. `Demo.Bff` 和 `Demo.Bff.ClientSecret` 是否可以并行运行？

可以。

因为它们已经区分了：

- 端口
- Cookie 名
- 客户端配置

### 2. 当前 Host 是否同时支持 `client_secret` 和 `private_key_jwt`？

支持。

当前同时支持：

- `client_secret_basic`
- `client_secret_post`
- `private_key_jwt`

### 3. 当前项目里谁负责 refresh token？

- Web 场景：由 BFF 服务端自动 refresh
- DeviceCode 场景：由控制台 demo 自动 refresh
- RefreshToken 场景：由独立 demo 手工演示

---

如果你要进一步做生产部署，可以在当前基础上继续扩展：

- K8s / Docker 部署配置
- 多副本 BFF
- 外部密钥管理
- JWKS 轮换
- 更细粒度的审计日志与告警
