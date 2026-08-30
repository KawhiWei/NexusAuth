# NexusAuth 文档

NexusAuth 是一个 .NET 10 OAuth 2.0 / OpenID Connect Provider。它提供统一登录、授权码、令牌签发、令牌刷新、设备授权和 OIDC 用户信息能力。

本文档以“能接入、能排错、知道边界”为目标。示例中的域名、客户端 ID 和密钥都是占位值，生产环境必须替换为自己的配置，并通过 Secret 管理密钥。

## 主手册

- [NexusAuth 使用手册（中文）](./12-使用手册.md)：从部署、初始管理员、管理台、OAuth/OIDC 接入、SCIM 2.0 到生产运维和排错的一体化手册。
- [NexusAuth User Guide (English)](../docs/en/user-guide.md)：与中文主手册章节对应的英文版。

## 当前能力

- `authorization_code`：Web/BFF 推荐流程；PKCE 是否必需由客户端 `require_pkce` 控制，但实际使用只接受 `S256`。
- `client_credentials`：机器到机器调用。
- `device_code`：受限设备登录和轮询。
- `refresh_token`：请求 `offline_access` 后签发，刷新时轮换旧 token。
- OIDC Discovery、JWKS、ID Token、UserInfo 和 RP-Initiated Logout。
- token endpoint 客户端认证：`client_secret_basic`、`client_secret_post`、`client_secret_jwt`、`private_key_jwt`。
- 授权端点 `response_mode=query` 和 `response_mode=form_post`。
- 已验证的授权错误安全回跳，并保留客户端 `state`。

认证失败遵循 OAuth 语义：`invalid_client` 使用 HTTP 401 并返回 `WWW-Authenticate`；缺少参数、格式错误等请求问题使用 `invalid_request`。授权端点只有在客户端和回调地址已安全确认后才会回跳错误；非法或未登记的回调地址不会被用于重定向。

## 接入边界

当前版本是机密客户端优先的 Provider。不要把 `client_secret`、JWT 私钥或 refresh token 放进浏览器 SPA、移动端或桌面端安装包。`token_endpoint_auth_method=none` 的 public client 目前未实现，公开客户端请通过 BFF 接入。

PAR、JAR、JARM、DPoP、OAuth mTLS/证书绑定令牌、动态客户端注册和 CIBA 等高级扩展也暂未实现。它们不属于当前发现文档中的可用能力。

## 文档导航

| 文档 | 说明 |
|------|------|
| [12 使用手册](./12-使用手册.md) | 管理员、应用接入方和运维人员的完整主手册。 |
| [01 快速开始](./01-快速开始.md) | 启动 Provider、数据库和 Workbench |
| [02 环境准备](./02-环境准备.md) | .NET、Node.js、PostgreSQL 和 Docker |
| [03 数据库配置](./03-数据库配置.md) | 初始化数据库和升级已有数据 |
| [04 启动 Provider](./04-启动NexusAuth.Provider.md) | 单独运行 SSO 服务 |
| [05 配置 OAuth 客户端](./05-配置OAuth客户端.md) | 客户端字段、PKCE、认证、curl 和 BFF 示例 |
| [06 对接 Workbench](./06-对接NexusAuth.Workbench.md) | Workbench API 的 OIDC 登录 |
| [07 启动 Dashboard](./07-对接NexusAuth.Workbench.Dashboard.md) | React Dashboard 启动和回调 |
| [08 高级配置](./08-高级配置.md) | Token、证书、HTTPS、代理和安全加固 |
| [09 常见问题](./09-常见问题.md) | 登录、PKCE、客户端认证和 Docker 排错 |
| [10 Demo 详解](./10-Demo示例详解.md) | 仓库内流程示例 |
| [11 OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md) | 验证顺序、错误决策和扩展边界 |

## 推荐接入流程

1. 启动 Provider 并读取 `/.well-known/openid-configuration`。
2. 在 Workbench 应用管理中登记客户端，使用精确的 `redirect_uri`。
3. Web/BFF 客户端设置 `require_pkce=true`、`client_secret_basic`，并申请最小 scope。
4. 生成 `state`、`nonce` 和 S256 PKCE 码对，跳转 `/connect/authorize`。
5. 回调拿到 `code` 后，在服务端调用 `/connect/token`；不要在浏览器兑换机密客户端的 code。
6. 将令牌保存在服务端会话或受保护的 HttpOnly Cookie 中，按需调用 refresh token。
7. 验证完毕后再使用 `userinfo`、撤销或登出端点。

协议细节和错误分支见 [11-OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md)，实际接入命令见 [05-配置 OAuth 客户端](./05-配置OAuth客户端.md)。

## 开发环境默认地址

| 服务 | 地址 |
|------|------|
| NexusAuth Provider | `http://localhost:5100` |
| Workbench API | `http://localhost:5051` |
| Workbench Dashboard | `http://localhost:5273` |
| PostgreSQL | `localhost:5432` |

开发账号和 seed 只用于本地测试。生产部署请使用 HTTPS、真实数据库凭据、受管签名证书，并关闭或移除 Demo 数据。

## 变更阅读顺序

第一次接入：先读 01、05、06；部署到生产：再读 08；排查标准兼容性或安全问题：阅读 09 和 11。
