# NexusAuth

NexusAuth 是一个基于 ASP.NET Core 和 .NET 10 的 OAuth 2.0 / OpenID Connect（OIDC）认证授权服务。它为 Workbench、业务 Web 应用和服务间调用提供统一登录、令牌签发与令牌校验能力。

它当前最适合 Web/BFF、服务端应用、机器到机器调用和受限设备流程。浏览器 SPA、移动端或桌面端这类无法安全保存客户端凭据的 public client 目前不应直接接入；建议通过 BFF 保护客户端密钥，并由 BFF 负责授权码兑换和 refresh token 保管。

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/React-19-blue" alt="React 19">
  <img src="https://img.shields.io/badge/PostgreSQL-16-blue" alt="PostgreSQL 16">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT">
</p>

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

Workbench 会跳转到 Provider 登录页，登录成功后回到 Dashboard。空数据库首次启动会执行初始化脚本和开发 seed；示例账号仅用于本地开发，不要带入生产环境。

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
