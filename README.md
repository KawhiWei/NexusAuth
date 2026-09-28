# NexusAuth

NexusAuth 是基于 ASP.NET Core / .NET 10 的 OAuth 2.0 授权服务器和 OpenID Connect（OIDC）身份提供方。接入方通过授权码获取用户身份，或用客户端凭据获取服务令牌；NexusAuth 负责登录、授权、签发和校验所需的协议端点。

当前接入对象是能安全保存凭据的服务端应用、Web 后端和设备客户端。`token_endpoint_auth_method=none` 尚未实现，SPA、移动端和桌面端不能把客户端密钥或 refresh token 放在自身环境中直接接入；Web 前端可通过服务端 BFF 完成协议交互。

## 从哪里开始

启动本地 Provider 后，先读取发现文档，不要在客户端硬编码端点：

```bash
docker compose up --build
curl http://localhost:5100/.well-known/openid-configuration
```

Provider 地址是 <http://localhost:5100>。根目录 Compose 还会启动 PostgreSQL 和一个接入示例（Workbench），但接入 NexusAuth 不依赖其管理页面或前端框架。全新数据库的结构来自 [production-init.sql](./production-init.sql)；Provider 根据环境变量创建初始管理员，Workbench 示例创建自己的 OAuth 客户端和服务资源。`demo/seed.sql` 不会自动执行。

Compose 默认的本地管理员是 `admin / wzw0126..`，只能用于开发。生产部署请先设置独立密码、HTTPS Issuer、数据库连接和签名材料；启动与完整配置见[快速启动](./document/01-快速启动.md)。

## 接入一个 Web 应用

以下步骤以服务端应用 `orders-bff` 调用 `orders-api` 为例。登记客户端时配置唯一的 `client_id`、精确的 `redirect_uri`（例如 `https://app.example.com/signin-oidc`）、`authorization_code` 和 `refresh_token` grant、`require_pkce=true`，以及一种 token endpoint 认证方式。服务资源 `orders.read` 的 audience 设为 `orders-api`，并关联到客户端。`openid`、`profile`、`offline_access` 是标准 scope，无需创建服务资源。客户端和服务资源可通过示例 Workbench 登记，具体字段见[应用接入手册](./document/01-快速启动.md#11-管理台与应用接入)。

1. 每次登录在服务端生成随机 `state`、`nonce` 和 PKCE `code_verifier`，保存到短期登录状态。浏览器仅携带由 verifier 计算的 S256 `code_challenge`，跳转发现文档返回的 `authorization_endpoint`：

   ```text
   /connect/authorize?response_type=code&client_id=orders-bff&redirect_uri=https%3A%2F%2Fapp.example.com%2Fsignin-oidc&scope=openid%20profile%20offline_access%20orders.read&state=RANDOM_STATE&nonce=RANDOM_NONCE&code_challenge=S256_CHALLENGE&code_challenge_method=S256
   ```

2. 用户在 NexusAuth 登录并同意授权后，Provider 把 `code` 和 `state` 返回给已登记的回调地址。服务端先校验 `state`，再用保存的 verifier 和登记的客户端凭据兑换授权码：

   ```bash
   curl -i -X POST http://localhost:5100/connect/token \
     -u 'orders-bff:REPLACE_WITH_CLIENT_SECRET' \
     -H 'Content-Type: application/x-www-form-urlencoded' \
     --data-urlencode 'grant_type=authorization_code' \
     --data-urlencode 'code=REPLACE_WITH_AUTHORIZATION_CODE' \
     --data-urlencode 'redirect_uri=https://app.example.com/signin-oidc' \
     --data-urlencode 'code_verifier=ORIGINAL_VERIFIER'
   ```

3. 请求包含 `openid` 时，兑换结果带 `id_token`。客户端须验证签名、`iss`、`aud`、有效期以及登录请求保存的 `nonce`；公钥来自发现文档的 `jwks_uri`。`id_token` 表示登录身份，不是业务 API 的访问凭据。业务 API 接收 `access_token`，按其 issuer、audience、签名、有效期和所需 scope 验证。需要用户资料时，用 access token 调用发现文档中的 `userinfo_endpoint`。

4. `offline_access` 同时出现在客户端允许范围和授权请求中、且客户端允许 `refresh_token` 时，Provider 才会签发 refresh token。刷新会轮换旧值，客户端必须保存响应中的新值；不要并发重用旧值。刷新响应不返回新的 ID Token。注销时清除应用自己的会话；需要结束 NexusAuth 的登录会话，再使用 `/connect/endsession`。主动撤销令牌用 `/connect/revocation`。

授权请求也支持 `response_mode=form_post`，上述 GET 回调示例使用默认的 `query`。`redirect_uri` 必须逐字符匹配，授权码一次性消费并绑定客户端与回调地址。PKCE 仅接受 `S256`。无法确认回调地址安全时，Provider 在本地返回错误，不向未登记地址重定向。完整请求、响应和错误处理见[协议接入章节](./document/01-快速启动.md#115-授权码--pkce-完整请求)及[协议设计](./document/11-OAuth-OIDC协议设计.md)。

## 其他授权方式

| 场景 | grant / 入口 | 使用要点 |
|---|---|---|
| 服务间调用 | `client_credentials`，`/connect/token` | 申请业务资源 scope；令牌代表客户端，不代表用户，不签发 ID Token。 |
| 受限输入设备 | `device_code`，`/connect/deviceauthorization` | 展示 `user_code` 和验证地址；按响应的 `interval` 轮询 token endpoint，处理 `authorization_pending`、`slow_down` 等结果。 |
| 延长用户授权 | `refresh_token`，`/connect/token` | 仅使用最新轮换出的 refresh token；失效时重新发起授权。 |

各流程的可运行客户端位于 `demo/src/`，启动方式见[Demo 示例](./document/01-快速启动.md#13-demo-示例)。

## 客户端认证与校验

每个客户端只能登记一种 token endpoint 认证方式，请求时不能混用：

| 方式 | 凭据 |
|---|---|
| `client_secret_basic` | HTTP Basic，共享密钥留在服务端。上面的兑换示例使用此方式。 |
| `client_secret_post` | 表单中的 `client_id` 和 `client_secret`。 |
| `client_secret_jwt` | 用共享密钥签名的 HS256 客户端断言。 |
| `private_key_jwt` | 用客户端私钥签名的 RS256 断言，Provider 使用登记的内嵌 JWKS 验签。 |

认证方式不匹配、凭据冲突或断言重放返回 `401 invalid_client`；缺少 `grant_type` 或 `code` 等普通参数返回 `invalid_request`。授权码、回调或 PKCE 校验失败通常返回 `invalid_grant`。客户端应区分这些错误，不能通过重试同一授权码解决问题。

Provider 发布 `/.well-known/jwks.json`，并提供 `/connect/introspect` 查询令牌状态。签名可用 X.509 PFX 或 RSA 密钥文件；轮换时可通过 `Jwt:SigningKeys` 同时发布新旧公钥，但配置变更需要重启。生产签名配置见[快速启动的密钥章节](./document/01-快速启动.md#91-签名材料与密钥轮换)。

## 已知边界

- 不支持 PAR、JAR、JARM、DPoP、OAuth mTLS、动态客户端注册、CIBA 或 public client 的 `none` 认证方式。
- 生产环境使用 HTTPS，精确登记回调地址，妥善保存客户端密钥、私钥、授权码和 refresh token；不要写入浏览器存储或日志。
- 用户登录可配置 TOTP、Passkey、自助注册和滑块验证码；这些是 Provider 登录环节的选项，不改变客户端的 OAuth/OIDC 接入方式。具体开关见[登录配置](./document/01-快速启动.md#43-host登录品牌验证码与-passkey)。

## 接入示例与运维文档

- [Workbench API 统一登录接入模板](./document/Workbench-API统一登录接入模板.md)：服务端 OIDC 客户端、回调、token 校验、会话和刷新。
- [Workbench Dashboard 统一登录接入模板](./document/Workbench-Dashboard统一登录接入模板.md)：浏览器如何通过同源后端完成登录，不接触 OAuth 凭据。
- [快速启动与部署](./document/01-快速启动.md)：镜像、数据库、全部环境变量、日志、管理操作和排错。
- [SMTP 发信试验](./document/SMTP发信试验.md)：可选内部发信服务，尚未接入注册验证或密码找回。
- [APISIX 网关对接](./document/13-APISIX网关对接手册.md)：当前非推荐部署流程，已有步骤仅供本地网关场景参考。
- [英文使用手册](./docs/en/user-guide.md)与[文档目录](./document/README.md)。

## 许可证

MIT License。
