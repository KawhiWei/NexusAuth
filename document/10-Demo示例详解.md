# Demo 示例详解

本文档详细介绍 NexusAuth 仓库中包含的所有 Demo 示例。

## Demo 列表

### 1. private_key_jwt 版 Web 登录

| 项目 | 说明 |
|------|------|
| 前端 | `demo/src/Demo.Web` |
| BFF | `demo/src/Demo.Bff` |
| client_id | `demo-bff` |
| 认证方式 | `private_key_jwt` |

**端口：**
- Frontend: `5200`
- BFF: `5201`

**启动方式：**

```bash
# 1. 启动 BFF
dotnet run --project demo/src/Demo.Bff

# 2. 启动前端
cd demo/src/Demo.Web
npm install
npm run dev

# 3. 访问 http://localhost:5200
```

### 2. client_secret_basic 版 Web 登录

| 项目 | 说明 |
|------|------|
| 前端 | `demo/src/Demo.Web.ClientSecret` |
| BFF | `demo/src/Demo.Bff.ClientSecret` |
| client_id | `demo-bff-secret` |
| 认证方式 | `client_secret_basic` |

**端口：**
- Frontend: `5300`
- BFF: `5301`

**启动方式：**

```bash
# 1. 启动 BFF
dotnet run --project demo/src/Demo.Bff.ClientSecret

# 2. 启动前端
cd demo/src/Demo.Web.ClientSecret
npm install
npm run dev

# 3. 访问 http://localhost:5300
```

### 3. client_credentials

| 项目 | 说明 |
|------|------|
| 项目 | `demo/src/Demo.ClientCredentials` |
| client_id | `demo-cc` |
| 认证方式 | `private_key_jwt` |

**启动方式：**

```bash
dotnet run --project demo/src/Demo.ClientCredentials
```

**作用：**
- 使用 `private_key_jwt` 换取 `access_token`
- 调用 BFF 的 `/api/m2m/profile`

### 4. device_code

| 项目 | 说明 |
|------|------|
| 项目 | `demo/src/Demo.DeviceCode` |
| client_id | `demo-device` |
| 认证方式 | `private_key_jwt` |

**启动方式：**

```bash
dotnet run --project demo/src/Demo.DeviceCode
```

**作用：**
1. 发起 device authorization
2. 输出 `user_code` 与 `verification_uri`
3. 轮询获取 `access_token` / `refresh_token`
4. 自动调用一次 BFF API
5. 自动测试一次 refresh token
6. 再次调用 BFF API

### 5. refresh_token

| 项目 | 说明 |
|------|------|
| 项目 | `demo/src/Demo.RefreshToken` |
| client_id | 复用 `demo-device` |
| 认证方式 | `private_key_jwt` |

**启动方式：**

```bash
dotnet run --project demo/src/Demo.RefreshToken
```

**作用：**
- 手工输入 `refresh_token`
- 单独验证 refresh token grant

## Demo 数据库初始化

### 完整初始化

```bash
psql -U nexusauth -d nexusauth -f demo/schema.sql
psql -U nexusauth -d nexusauth -f demo/seed.sql
```

### 测试账号

- `alice / Pass@123`
- `bob / Pass@123`
- `admin / Pass@123`

## Test 时间设置

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

- `CookieLifetimeMinutes = 3`

**说明：**
- access token 1 分钟后过期
- refresh token 3 分钟后过期
- Cookie 会话 3 分钟有效

这样既能测到 refresh，又不会因为 Cookie 过短导致会话先失效。

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

4. 若即将��期且存在 refresh token，则自动调用 `/connect/token`

5. 拿到新 token 后自动更新 Cookie Session

6. 前端无感知，业务请求继续执行

**实现位置：**
- `demo/src/Demo.Bff/Services/OidcSessionCookieEvents.cs`
- `demo/src/Demo.Bff/Services/OidcBffService.cs`

### DeviceCode 场景

`Demo.DeviceCode` 现在已经自动串联 refresh token：

1. device authorization 成功
2. 首次 access token 调 API
3. 自动 refresh token
4. 使用刷新后的 access token 再次调 API

## 并行运行 Demo

`Demo.Bff` 和 `Demo.Bff.ClientSecret` 可以并行运行。

因为它们已经区分了：
- 端口
- Cookie 名
- 客户端配置