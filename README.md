# NexusAuth

`Nexus` 意为"连接点"，隐喻统一认证中心作为多个系统之间的连接枢纽。

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9.0-blue" alt=".NET 9.0">
  <img src="https://img.shields.io/badge/React-19-blue" alt="React 19">
  <img src="https://img.shields.io/badge/PostgreSQL-16-blue" alt="PostgreSQL 16">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT">
</p>

NexusAuth 是一个基于 ASP.NET Core 的 OAuth 2.0 / OpenID Connect 认证授权服务，支持为多个前端应用提供统一的登录认证。

## 核心能力

### OAuth 2.0 授权模式

- ✅ `authorization_code + PKCE` - 推荐，用于Web/移动应用
- ✅ `client_credentials` - 机器到机器调用
- ✅ `device_code` - 设备授权
- ✅ `refresh_token` - Token 自动续期

### OpenID Connect 能力

- ✅ `/.well-known/openid-configuration` - OIDC 发现
- ✅ `/.well-known/jwks.json` - JSON Web Key Set
- ✅ `userinfo` - 用户信息端点
- ✅ `id_token` - ID Token
- ✅ `end_session` - 登出端点

### 客户端认证方式

- ✅ `client_secret_basic`
- ✅ `client_secret_post`
- ✅ `private_key_jwt`

### 安全增强

- ✅ `authorization_code` 强制 `S256 PKCE`
- ✅ `private_key_jwt` 强制 `RS256`
- ✅ `private_key_jwt` 支持 `jti` 防重放
- ✅ refresh token 与 `client_id` 绑定
- ✅ revocation / introspection 客户端边界检查

## 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                   NexusAuth OAuth Provider                  │
│                       (Port: 5100)                        │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────────────┐  │
│  │  /connect/  │ │  /.well-    │ │  /account/         │  │
│  │  authorize │ │  known/     │ │  login             │  │
│  └─────────────┘ └─────────────┘ └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         │                  │                  │
         ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   Your App 1   │ │   Your App 2   │ │ Workbench API   │
│    (Frontend)  │ │   (Mobile)    │ │ & Dashboard    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## 快速导航

| 文档 | 说明 |
|------|------|
| [快速开始](./document/01-快速开始.md) | 5分钟快速部署 |
| [环境准备](./document/02-环境准备.md) | 开发环境要求 |
| [配置 OAuth 客户端](./document/05-配置OAuth客户端.md) | 添加客户端应用 |
| [对接 Workbench API](./document/06-对接NexusAuth.Workbench.Api.md) | 对接后端服务 |
| [对接 Dashboard](./document/07-对接NexusAuth.Workbench.Dashboard.md) | 对接前端应用 |
| [常见问��](./document/09-常见问题.md) | FAQ |

## 项目组成

### 核心服务

| 目录 | 说明 | 端口 |
|------|------|------|
| `src/NexusAuth.Host` | OAuth Provider (认证服务) | 5100 |
| `src/NexusAuth.Extension` | 通用扩展类库 | - |

### OAuth 管理端（Workbench）

NexusAuth.Workbench 是基于 NexusAuth 构建的管理端站点，包含：

| 目录 | 说明 | 端口 |
|------|------|------|
| `src/NexusAuth.Workbench.Api` | 后端服务 (BFF) | 5051 |
| `src/NexusAuth.Workbench.Dashboard` | 前端仪表板 | 5273 |

**使用方式：**
1. 配置 PostgreSQL 数据库
2. 执行 `seed.sql` 插入基础数据
3. 启动服务即可

详细见 [对接 NexusAuth.Workbench.Api](./document/06-对接NexusAuth.Workbench.Api.md)

## Demo 示例

完整 Demo 示例见 [demo](./demo) 目录：

| Demo | 说明 |
|------|------|
| `Demo.Web` + `Demo.Bff` | private_key_jwt 版 Web 登录 |
| `Demo.Web.ClientSecret` + `Demo.Bff.ClientSecret` | client_secret_basic 版 Web 登录 |
| `Demo.ClientCredentials` | client_credentials 机器调用 |
| `Demo.DeviceCode` | device_code 设备授权 |
| `Demo.RefreshToken` | 独立 refresh_token 测试 |

Demo 详细文档移至 [document/10-Demo示例详解.md](./document/10-Demo示例详解.md)

## 数据库初始化

### Demo 环境

```bash
psql -U nexusauth -d nexusauth -f demo/schema.sql
psql -U nexusauth -d nexusauth -f demo/seed.sql
```

测试账号：`alice / Pass@123`、`bob / Pass@123`、`admin / Pass@123`

### 生产环境

```bash
psql -U nexusauth -d nexusauth -f production-init.sql
```

## 启动服务

### 1. 启动 OAuth Provider

```bash
dotnet run --project src/NexusAuth.Host
# 访问 http://localhost:5100
```

### 2. 启动 Workbench（推荐用于开发）

```bash
# 启动后端
dotnet run --project src/NexusAuth.Workbench.Api

# 启动前端
cd src/NexusAuth.Workbench.Dashboard
npm install && npm run dev

# 访问 http://localhost:5273
```

## 当前核心客户端

| client_id | auth method | grant types | 说明 |
| --- | --- | --- | --- |
| `demo-bff` | `private_key_jwt` | `authorization_code`, `refresh_token` | Web + BFF |
| `demo-bff-secret` | `client_secret_basic` | `authorization_code`, `refresh_token` | Web + BFF |
| `demo-cc` | `private_key_jwt` | `client_credentials` | 机器到机器 |
| `demo-device` | `private_key_jwt` | `device_code`, `refresh_token` | 设备授权 |

## Mermaid 时序图

架构图和流程图移至 [document/diagrams](./document/diagrams/)：

- `architecture-overview.mmd` - 系统架构
- `architecture-layered.mmd` - 分层架构
- `auth-sequence-authorization-code.mmd` - 授权码流程
- `auth-sequence-client-credentials.mmd` - 客户端凭证流程
- `auth-sequence-device-code.mmd` - 设备授权流程

## 生产化建议

1. **私钥管理** - 不要把 `.pem` 提交到仓库，使用 K8s Secret / Vault
2. **客户端独立 Key/JWKS** - 每个 client 使用独立 kid，各自独立 JWKS
3. **生产初始化** - 使用 `production-init.sql`，不要带 demo 数据
4. **BFF 多副本** - 考虑 Cookie / DataProtection key 共享
5. **日志审计** - token issuance、refresh、revocation 等事件

## 常见问题

完整 FAQ 见 [常见问题](./document/09-常见问题.md)

### 1. Demo 系列是否可以并行运行？

可以。它们已区分端口和 Cookie 名。

### 2. Host 是否同时支持 client_secret 和 private_key_jwt？

支持。当前同时支持 `client_secret_basic`、`client_secret_post`、`private_key_jwt`。

### 3. 谁负责 refresh token？

- Web 场景：由 BFF 服务端自动 refresh
- DeviceCode 场景：由控制台 demo 自动 refresh
- RefreshToken 场景：独立 demo 手工演示

---

如果需要进一步开发或参与贡献，欢迎提交 Issue 和 Pull Request！

**许可证**：MIT