# 启动 NexusAuth Provider

Provider 是 NexusAuth 的 OAuth 2.0 / OpenID Connect 服务，默认地址为 `http://localhost:5100`。它负责登录页、授权、令牌、OIDC Discovery、SCIM 和服务端 SSO 会话。

## 1. 本地启动

先准备 PostgreSQL 和空库 schema，具体步骤见 [03 数据库配置](./03-数据库配置.md)。在仓库根目录设置必要配置后启动：

```bash
export NEXUSAUTH_CONNECTION_STRINGS_DEFAULT='Host=localhost;Port=5432;Database=nexusauth;Username=nexusauth;Password=REPLACE_WITH_DATABASE_PASSWORD;Search Path=nexusauth'
export NEXUSAUTH_JWT_ISSUER='http://localhost:5100'
export NEXUSAUTH_BOOTSTRAP_ADMIN_USERNAME=admin
export NEXUSAUTH_BOOTSTRAP_ADMIN_PASSWORD='REPLACE_WITH_ADMIN_PASSWORD'

dotnet run --project src/NexusAuth.Host
```

初始管理员的用户名和密码必须同时设置；用户已存在时，启动过程不会覆盖其密码。开发环境缺少签名材料时会生成并持久化开发签名证书。生产环境不会生成证书，必须提供受管的 PFX 或 RSA 私钥。

## 2. 常用环境变量

Provider 使用 `NEXUSAUTH_` 前缀的单下划线变量，而不是要求部署系统传递 .NET 双下划线键：

| 环境变量 | 说明 |
|---|---|
| `NEXUSAUTH_CONNECTION_STRINGS_DEFAULT` | PostgreSQL 连接字符串 |
| `NEXUSAUTH_JWT_ISSUER` | 对外 Provider 地址；生产必须为 HTTPS |
| `NEXUSAUTH_JWT_SIGNING_MODE` | `Certificate` 或兼容模式 `RsaKeyFile` |
| `NEXUSAUTH_JWT_SIGNING_CERTIFICATE_PATH` / `...PASSWORD` | 生产 PFX 路径与密码 |
| `NEXUSAUTH_JWT_SIGNING_KEY_PATH` | `RsaKeyFile` 模式的生产私钥路径 |
| `NEXUSAUTH_BOOTSTRAP_ADMIN_*` | 初始系统管理员资料 |
| `NEXUSAUTH_LOGIN_FLOW_REMEMBER_ME_LIFETIME_DAYS` | 登录页“几天内免登录”的固定期限，默认 3，范围 1-30 |

完整配置、证书和反向代理要求见 [08 高级配置](./08-高级配置.md)。修改环境变量后重启 Provider。

## 3. 登录会话

登录页默认提供“3 天内免登录”。勾选后，持久 Cookie 与服务端 SSO session 使用同一固定期限，不会滑动续期；未勾选时使用 `LoginFlow:SessionLifetimeMinutes` 的常规会话策略。把 `NEXUSAUTH_LOGIN_FLOW_REMEMBER_ME_LIFETIME_DAYS` 改为 1-30 的整数后，页面文案和新签发会话都会使用新值；已经签发的 Cookie 不会被延长。

## 4. 主要端点

| 端点 | 方法 | 用途 |
|---|---|---|
| `/.well-known/openid-configuration` | GET | OIDC Discovery |
| `/.well-known/jwks.json` | GET | JWT 签名公钥 |
| `/connect/authorize` | GET | 授权码请求 |
| `/connect/token` | POST | 授权码、refresh token、client credentials 和 device code 换 token |
| `/connect/userinfo` | GET | OIDC UserInfo |
| `/connect/deviceauthorization` | POST | Device Authorization |
| `/connect/introspect` | POST | Token introspection |
| `/connect/revocation` | POST | Token revocation |
| `/connect/endsession` | GET | RP-Initiated Logout |
| `/account/login` | GET/POST | Provider 登录页 |
| `/scim/v2` | 多种 | SCIM 2.0 用户供给 |

不要在客户端中硬编码以上端点。接入方应先读取 Discovery 文档，再使用其中返回的端点地址。

## 5. 生产检查

- `NEXUSAUTH_JWT_ISSUER`、反向代理公开地址、Discovery `issuer` 和客户端登记的回调地址必须一致。
- 配置 `NEXUSAUTH_SSO_ENVIRONMENT=Production`（Compose）并预置受管签名证书或 RSA 私钥。
- 将 Data Protection key ring 持久化并在 Provider 多副本之间共享；否则 Cookie 和 TOTP 密钥无法跨实例验证。
- 不要使用 Compose 的开发管理员、数据库密码或签名证书密码。
- 使用 `/connect/authorize` 和 OIDC 客户端流程验证登录；不要通过直接写库创建生产用户。

继续阅读：[05 配置 OAuth 客户端](./05-配置OAuth客户端.md)、[06 对接 Workbench](./06-对接NexusAuth.Workbench.md)、[11 OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md)。
