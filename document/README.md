# NexusAuth 文档

NexusAuth 是一个基于 .NET 10 的 OAuth 2.0 / OpenID Connect Provider，提供统一登录、授权码、令牌签发与刷新、设备授权、UserInfo、令牌撤销、RP-Initiated Logout 和 SCIM 2.0 用户管理能力。

本文档以“能启动、能接入、知道边界”为目标。示例中的域名、客户端 ID、密码和密钥都是占位值，生产环境必须替换，并通过 Secret 或密钥管理系统保存。

## 从哪里开始

- 想在本地运行全部服务：阅读 [01 快速开始](./01-快速开始.md)。
- 只运行 Provider：阅读 [04 启动 Provider](./04-启动NexusAuth.Provider.md)。
- 接入业务应用：阅读 [05 配置 OAuth 客户端](./05-配置OAuth客户端.md)。
- 使用管理端：依次阅读 [06 对接 Workbench](./06-对接NexusAuth.Workbench.md) 和 [07 启动 Dashboard](./07-对接NexusAuth.Workbench.Dashboard.md)。
- 生产配置与安全加固：阅读 [08 高级配置](./08-高级配置.md) 和 [12 使用手册](./12-使用手册.md)。

完整导航：

| 文档 | 内容 |
|------|------|
| [01 快速开始](./01-快速开始.md) | Docker Compose、本地进程和默认地址 |
| [02 环境准备](./02-环境准备.md) | .NET、Node.js、PostgreSQL、Docker 和目录结构 |
| [03 数据库配置](./03-数据库配置.md) | 空库初始化、seed、schema 和升级边界 |
| [04 启动 Provider](./04-启动NexusAuth.Provider.md) | 签名材料、初始管理员、登录流和 Provider 端点 |
| [05 配置 OAuth 客户端](./05-配置OAuth客户端.md) | 客户端字段、API resource、PKCE、客户端认证和 BFF |
| [06 对接 Workbench](./06-对接NexusAuth.Workbench.md) | Workbench API 的 OIDC BFF、Cookie 和环境变量 |
| [07 启动 Dashboard](./07-对接NexusAuth.Workbench.Dashboard.md) | React 前端、代理、路由和生产构建 |
| [08 高级配置](./08-高级配置.md) | Token、证书、HTTPS、代理和安全加固 |
| [09 常见问题](./09-常见问题.md) | 登录、PKCE、客户端认证和 Docker 排错 |
| [10 Demo 详解](./10-Demo示例详解.md) | 仓库内授权码、设备码和令牌示例 |
| [11 OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md) | 验证顺序、错误语义和扩展边界 |
| [12 使用手册](./12-使用手册.md) | 部署、管理台、OAuth/OIDC、SCIM 和运维总览 |

## 当前支持的能力

- `authorization_code`：Web/BFF 推荐流程；支持 PKCE，实际 challenge method 只接受 `S256`。
- `client_credentials`：机器到机器调用，只能申请业务 API resource scope。
- `urn:ietf:params:oauth:grant-type:device_code`：受限设备登录和轮询。
- `refresh_token`：授权请求申请 `offline_access` 后签发，刷新时轮换旧 token。
- OIDC Discovery、RS256 JWKS、ID Token、UserInfo 和 RP-Initiated Logout。
- Token endpoint 客户端认证：`client_secret_basic`、`client_secret_post`、`client_secret_jwt`、`private_key_jwt`。
- 授权端点 `response_mode=query` 和 `response_mode=form_post`。
- `prompt=none`、`prompt=login`、`prompt=consent`、`max_age` 和 OIDC `claims` 参数。
- SCIM 2.0 的用户查询、创建、更新、Patch、删除和服务配置端点。

Provider 的发现文档是 `/.well-known/openid-configuration`，签名公钥地址是 `/.well-known/jwks.json`。自定义 API resource 的 scope 不会固定写入 Discovery 的 `scopes_supported`，应以客户端登记和 Workbench 的服务资源配置为准。

认证失败遵循 OAuth 语义：`invalid_client` 使用 HTTP 401 并返回 `WWW-Authenticate`；缺少参数或格式错误使用 `invalid_request`。授权端点只有在客户端和回调地址已安全确认后才会回跳错误；非法或未登记的回调地址不会用于重定向。

## 接入边界

当前版本是机密客户端优先的 Provider。不要把 `client_secret`、JWT 私钥或 refresh token 放进浏览器 SPA、移动端或桌面端安装包。`token_endpoint_auth_method=none` 的 public client 未实现，公开客户端请通过 BFF 接入。

PAR、JAR、JARM、DPoP、OAuth mTLS/证书绑定令牌、动态客户端注册和 CIBA 等高级扩展暂未实现，也不会出现在当前 Discovery 的可用能力中。

## 数据库和初始化边界

Provider 和 Workbench API 共用 `nexusauth` PostgreSQL 数据库及 `nexusauth` schema。`production-init.sql` 是全新数据库的当前最终 schema：它创建表、索引和 `pgcrypto` 扩展，但不会创建数据库、默认用户或 Demo 数据。

Docker Compose 仅在全新的 PostgreSQL 数据卷上执行数据库初始化，并额外执行 Workbench 的 `seed.sql`。Workbench seed 登记 API resource 和 OAuth client，不写入 client secret；Workbench API 启动时从 `Auth:ClientSecret` 同步该凭据。

当前仓库不提供历史数据库的增量迁移脚本。升级前应备份数据，并根据 [03 数据库配置](./03-数据库配置.md) 的边界在新实例完成初始化和数据迁移。

## 关键默认值

| 项目 | 默认值 |
|------|--------|
| Provider | `http://localhost:5100` |
| Workbench API | `http://localhost:5051` |
| Workbench Dashboard | `http://localhost:5273` |
| PostgreSQL | `localhost:5432` |
| access token | 60 分钟 |
| refresh token | 43200 分钟 |
| 登录页免登录期限 | 3 天 |

登录页的“3 天内免登录”由 `NEXUSAUTH_LOGIN_FLOW_REMEMBER_ME_LIFETIME_DAYS` 配置，允许 1-30 天。勾选后 Cookie 和服务端 SSO session 使用固定期限且不滑动续期；修改变量后需要重启 Provider，已有 Cookie 不会被延长。

## 推荐接入流程

1. 启动 Provider，并读取 `/.well-known/openid-configuration`。
2. 在 Workbench 应用管理中登记客户端、精确的回调地址和最小 scope。
3. Web/BFF 设置 `require_pkce=true`、`client_secret_basic`，并在服务端保存凭据。
4. 每次登录生成新的 `state`、`nonce` 和 S256 PKCE 码对，跳转 `/connect/authorize`。
5. 回调拿到 `code` 后，在服务端调用 `/connect/token`；不要在浏览器兑换机密客户端的 code。
6. 将 token 保存在服务端会话或受保护的 HttpOnly Cookie 中，按需轮换 refresh token。
7. 使用 access token 调用业务 API 或 `/connect/userinfo`，必要时使用撤销和登出端点。

协议细节见 [11 OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md)，可复制的接入请求见 [05 配置 OAuth 客户端](./05-配置OAuth客户端.md)。

## 其他版本

- [NexusAuth 使用手册（中文）](./12-使用手册.md)：部署、管理、接入和运维的主手册。
- [NexusAuth User Guide (English)](../docs/en/user-guide.md)：英文版主手册。
