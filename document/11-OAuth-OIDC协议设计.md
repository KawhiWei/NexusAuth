# OAuth 2.0 / OIDC 协议设计

本文是 NexusAuth 当前 OAuth 2.0 / OpenID Connect Provider 的行为说明。它解释授权请求如何校验、错误何时回跳、PKCE 如何按客户端生效，以及 token endpoint 如何处理客户端认证冲突。

本文描述的是实现边界，不代表 NexusAuth 已实现所有 OAuth/OIDC 扩展。接入方可先阅读 [使用手册](./12-使用手册.md) 的接入章节，再按需查看 [Workbench 接入说明](./06-对接NexusAuth.Workbench.md) 和 [Demo 示例](./10-Demo示例详解.md)。

## 0. 端点总览

Provider 默认地址为 `http://localhost:5100`；生产环境应使用 HTTPS Issuer。`/connect/token`、`/connect/deviceauthorization`、`/connect/introspect` 和 `/connect/revocation` 这几个 POST 端点要求 `application/x-www-form-urlencoded`；授权、登出等 GET 端点使用查询参数，UserInfo 使用 Bearer 头，并在需要客户端身份的端点提供已登记的客户端认证。

| 方法 | 端点 | 用途 | 客户端认证 |
|---|---|---|---|
| GET | `/.well-known/openid-configuration` | OIDC Discovery | 否 |
| GET | `/.well-known/jwks.json` | Provider 签名公钥 | 否 |
| GET | `/connect/authorize` | Authorization Code 授权请求 | 否，用户在 Provider 会话中登录 |
| POST | `/connect/token` | 授权码、client credentials、device code、refresh token 换取令牌 | 是 |
| GET | `/connect/userinfo` | 使用 Bearer access token 读取用户信息 | Bearer token |
| POST | `/connect/deviceauthorization` | 创建设备授权请求 | 是 |
| POST | `/connect/introspect` | 检查 access token 或 ID token 状态 | 是 |
| POST | `/connect/revocation` | 撤销 access token 或 refresh token | 是 |
| GET | `/connect/endsession` | OIDC RP-Initiated Logout | 按 `id_token_hint` 校验 |
| GET/POST | `/device` | 用户输入设备码并批准/拒绝 | Provider Cookie |
| GET/POST | `/account/login` | Provider 用户登录 | Provider Cookie |

Discovery 的 `scopes_supported` 只列标准身份 scope：`openid`、`profile`、`email`、`phone`、`address` 和 `offline_access`。业务 API scope 由客户端与 API resource 的登记关系决定，不应假设所有业务 scope 都会出现在 Discovery 中。

## 1. 设计目标

NexusAuth 的核心目标是让 Web/BFF、服务端应用、机器客户端和受限设备可以使用一套可验证、可审计的认证流程：

- 只把授权响应发送到已登记且精确匹配的回调地址；
- 将客户端认证、授权请求校验和令牌校验分成清晰的阶段；
- 对错误使用 OAuth 标准错误码和 HTTP 语义；
- 在安全可回跳时保留 `state`，在不安全时宁可停留在 Provider 页面；
- 以客户端配置为边界启用 PKCE，不用一个全局开关覆盖所有客户端；
- 不通过隐式降级来掩盖凭据冲突、签名算法错误或错误的认证方式。

当前支持授权码、client credentials、device code、refresh token、OIDC Discovery/JWKS/UserInfo/ID Token/RP-Initiated Logout，以及 `client_secret_basic`、`client_secret_post`、`client_secret_jwt`、`private_key_jwt`。当前不支持 public client `none`、PAR、JAR、JARM、DPoP、mTLS、动态注册和 CIBA。

## 2. 授权请求验证顺序

授权端点是 `/connect/authorize`，当前支持 `response_type=code`。为了避免把错误回传给攻击者控制的地址，验证顺序有意分为“安全边界”和“业务条件”两个阶段：

1. **读取安全边界参数**：先读取 `client_id` 和 `redirect_uri`。缺少任意一项时，本地返回 `invalid_request`，不会向请求中的地址回跳。
2. **查找客户端**：客户端必须存在且启用。未知或停用的 `client_id` 不能安全回跳，因为请求还没有建立可信的客户端上下文。
3. **精确验证回调地址**：`redirect_uri` 必须与客户端登记值逐字匹配。协议、Host、端口、路径和大小写都不能被模糊比较。匹配成功后，才建立“允许错误回跳”的安全边界。
4. **验证响应类型和模式**：只接受 `response_type=code`；`response_mode` 只接受 `query` 或 `form_post`。回调地址已经确认安全但模式未知时，Provider 按默认 `query` 回传 `invalid_request`；这不会采用请求中未知的响应方式。
5. **验证 grant 和 scope**：客户端必须允许 `authorization_code`；每个 scope 必须在客户端允许列表中，并且对应的资源处于启用状态。非法 scope 返回 `invalid_scope`，而不是让应用层异常冒泡成 HTTP 500。
6. **按客户端验证 PKCE**：读取客户端的 `require_pkce`，检查 `code_challenge` 与 `code_challenge_method`。只接受 `S256`，不接受 `plain` 或未知方法。
7. **验证 OIDC 参数**：解析 `claims` JSON，检查 `prompt` 组合和 `max_age` 等条件。无效 JSON 或互斥 prompt 返回 `invalid_request`。
8. **处理用户会话和同意**：无登录会话时进入登录页；`prompt=none` 无法静默完成时，安全回跳 `login_required`。需要同意时进入确认页，并将已校验的授权上下文带回流程。
9. **签发一次性授权码**：授权码绑定 `client_id`、`redirect_uri`、scope、用户、PKCE challenge 和 OIDC 上下文，并设置过期时间。
10. **构造响应**：成功时返回 `code` 和原始 `state`。如果业务步骤在安全边界之后失败，返回同样的错误字段和 `state`，但永远不把 token 或 verifier 放进 URL/表单。

其中第 2、3 步决定“能不能回跳”，第 4 步以后决定“回跳什么错误”。这能同时避免开放重定向和非法 scope 导致的异常。

## 3. 授权错误响应决策

### 3.1 安全回跳条件

只有同时满足以下条件时，Provider 才把错误发送到客户端：

- 找到了启用的客户端；
- `redirect_uri` 与登记值精确匹配；
- 错误发生在上述安全边界建立之后。

如果 `response_mode` 缺失，Provider 使用默认的 `query`。如果它是未知值，但客户端和回调地址已经通过验证，Provider 同样使用默认 `query` 安全回传 `invalid_request`，不会执行未知模式。

安全回跳包含：

```text
error=<OAuth error code>
error_description=<human-readable description, optional>
state=<original state, when supplied>
```

`state` 只在安全回跳时原样带回，客户端必须把它与登录开始时保存的值比较。Provider 不会把 `client_secret`、授权码、access token、refresh token 或 `code_verifier` 放到错误响应中。

### 3.2 不安全请求

以下情况不回跳：缺少 `client_id` 或 `redirect_uri`、未知客户端、客户端停用、`redirect_uri` 不匹配，或者无法证明回调地址已经登记。Provider 直接返回本地 HTTP 错误页面或 JSON 错误。

这是有意的安全选择：即使请求中带有 `state`，也不能为了回传错误而信任一个尚未验证的回调地址。

### 3.3 错误码和 HTTP 语义

| 阶段 | 错误码 | 典型原因 | 是否回跳 |
|------|--------|----------|----------|
| 授权请求 | `invalid_request` | 缺少参数、非法 `response_mode`、PKCE 参数格式错误、claims JSON 无效 | 回调已安全确认时回跳，否则本地返回 |
| 授权请求 | `invalid_scope` | 请求 scope 不在客户端允许列表或资源未启用 | 同上 |
| 授权请求 | `unauthorized_client` | 客户端未允许 `authorization_code` | 同上 |
| 授权请求 | `unsupported_response_type` | `response_type` 不是 `code` | 已安全确认时回跳，否则本地返回 |
| 静默登录 | `login_required` | `prompt=none` 但没有有效登录或 `max_age` 已过期 | 安全回跳 |
| token endpoint | `invalid_client` | 客户端不存在、凭据错误、认证方式不匹配或认证冲突 | HTTP 401，不使用授权回调 |
| token endpoint | `invalid_grant` | 授权码已使用/过期、回调地址不一致、PKCE verifier 错误 | HTTP 400 |
| token endpoint | `invalid_request` | 缺少 `grant_type`、`code` 或 `refresh_token` | HTTP 400 |
| token endpoint | `unauthorized_client` | 客户端未允许当前 grant | HTTP 400 |

token endpoint 的 `invalid_client` 必须返回 `401 Unauthorized` 和 `WWW-Authenticate`，例如：

```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Basic realm="NexusAuth"
Content-Type: application/json

{"error":"invalid_client","error_description":"Invalid client authentication."}
```

客户端应根据 `error` 和状态码处理，不应依赖描述文字。`WWW-Authenticate` 的 challenge 用于说明需要客户端认证，不代表服务端会接受任意认证方式。

## 4. PKCE 状态矩阵

PKCE 策略属于客户端元数据。Provider 不应把所有客户端都硬编码为同一种行为，也不应因为 `require_pkce=false` 就关闭已提交的 challenge 校验。

| 客户端配置 | 授权请求 | 授权码存储 | token 请求 | 结果 |
|---|---|---|---|---|
| `require_pkce=true` | 有 `S256` challenge | 存储 challenge 和 method | 必须有正确 verifier | 成功 |
| `require_pkce=true` | 缺 challenge | 不签发 code | - | `invalid_request` |
| `require_pkce=true` | method 为 `plain`/未知 | 不签发 code | - | `invalid_request` |
| `require_pkce=false` | 没有 challenge | code 不含 PKCE 约束 | 可省略 verifier | 仅可信机密客户端建议使用 |
| `require_pkce=false` | 有 `S256` challenge | 存储 challenge 和 method | 必须有正确 verifier | 成功 |
| 任意配置 | 有 `plain`/未知 method | 不签发 code | - | `invalid_request` |
| 已存 challenge | 任意 | 已绑定 challenge | verifier 缺失或不匹配 | `invalid_grant` |

`code_challenge` 不能代替 `state`，`state` 不能代替 PKCE。前者保护授权码兑换，后者让客户端确认回调属于当前浏览器会话；OIDC 客户端还应使用 `nonce` 关联 ID Token。

## 5. Token Endpoint 行为

`POST /connect/token` 只接受 `application/x-www-form-urlencoded`。请求先解析并验证客户端身份，再按 `grant_type` 进入对应分支；普通业务参数错误不会被包装成 `invalid_client`。

### authorization_code

请求至少包含 `grant_type=authorization_code`、`code` 和与授权请求完全一致的 `redirect_uri`。授权码在数据库中只保存 SHA-256 哈希，绑定客户端、用户、scope、回调地址、PKCE challenge 和 OIDC 上下文，默认有效期为 10 分钟，并且只能成功消费一次。

如果授权请求包含 `openid`，响应会包含 ID Token；如果 scope 包含 `offline_access` 且客户端允许 `refresh_token`，响应会包含 refresh token。缺少或错误的 `code_verifier`、授权码已使用、过期、客户端不匹配或回调地址不一致，通常返回 `invalid_grant`。

### client_credentials

请求包含 `grant_type=client_credentials` 和客户端允许的 `scope`。该流程代表客户端而不是用户，只签发 access token，不签发 ID Token 或 refresh token。客户端仍必须通过登记的认证方式完成认证，并且 scope 必须属于其允许的 API resources。

### refresh_token

请求包含 `grant_type=refresh_token` 和 `refresh_token`，客户端必须重新认证。Provider 会检查 refresh token 的客户端归属、用户状态和绝对过期时间，然后在数据库事务中撤销旧 token、写入新 token 并签发新的 access token。客户端必须原子地保存新 refresh token，旧值再次使用会失败。

### device_code

设备流程使用 grant type `urn:ietf:params:oauth:grant-type:device_code`，请求包含 `device_code` 并重新完成客户端认证。设备码状态包括：

- `authorization_pending`：用户还没有完成批准；
- `slow_down`：轮询过于频繁，客户端应按响应增加间隔；
- `expired_token`：设备码超过生命周期；
- `access_denied`：用户拒绝授权；
- 成功后设备码被一次性消费，不能再次兑换。

Provider 默认设备码有效期为 15 分钟，实际值由 `Jwt:DeviceCodeLifetimeMinutes` 决定。设备客户端应使用响应中的 `interval`，并遵循 `Retry-After`，不要固定高频请求。

## 6. 客户端认证互斥规则

客户端在注册时选择一个 `token_endpoint_auth_method`。请求进入 token、introspection、revocation 或 device authorization 这类需要客户端身份的端点后，Provider 按以下规则处理：

| 注册方式 | 合法请求形态 | 关键验证 |
|------|------|------|
| `client_secret_basic` | `Authorization: Basic base64(client_id:client_secret)` | Basic 解码成功、client ID 存在、密钥正确。 |
| `client_secret_post` | 表单 `client_id` + `client_secret` | 表单身份与客户端注册方式一致、密钥正确。 |
| `client_secret_jwt` | 表单 `client_assertion_type` + `client_assertion` | `HS256`、`iss/sub`、`aud`、`exp`、`jti` 和共享密钥均正确。 |
| `private_key_jwt` | 表单 `client_assertion_type` + `client_assertion` | `RS256`、`iss/sub`、`aud`、`exp`、`jti` 和已登记 JWKS 均正确。 |

以下情况一律拒绝为 `invalid_client`，不会静默回退：

- Basic 头存在，同时又提供不同的表单 `client_id` 或 `client_secret`；
- 提供 `client_secret`，同时提供 JWT assertion；
- 提供多个互相矛盾的客户端身份来源；
- Basic 编码非法、分隔符缺失或 assertion 类型不正确；
- 客户端登记为一种认证方式，却使用另一种方式；
- assertion 的签名算法、audience、issuer、subject、有效期或 `jti` 不正确；
- 同一个 `jti` 在有效期内重复使用。

普通业务参数错误不应伪装成认证错误：缺少 `grant_type`、`code`、`refresh_token` 或 `device_code` 使用 `invalid_request`；客户端身份已识别但未获准使用 grant 时使用 `unauthorized_client`；客户端身份无法验证时使用 `invalid_client` 和 HTTP 401。

## 7. 响应模式实现

### query

成功响应使用 HTTP 302，将 `code` 和 `state` 作为查询参数追加到已经登记的回调地址：

```text
https://client.example.com/signin-oidc?code=...&state=...
```

错误也使用查询参数，但只在安全回跳条件满足时执行。

### form_post

成功或安全错误响应使用一个不包含外部脚本的自动提交 HTML form，把字段以 POST 发送到登记的回调地址。客户端必须对 `application/x-www-form-urlencoded` POST 做 CSRF/状态校验，不能仅凭“收到 POST”就建立登录会话。

`response_mode=form_post` 不是 JARM：表单字段仍是普通 OAuth 响应参数，不是签名的 JWT。JARM 当前未实现。

## 8. OIDC 能力与登出

### Discovery、ID Token 和 UserInfo

OIDC Discovery 通过 `/.well-known/openid-configuration` 发布授权、token、UserInfo、JWKS、设备授权、introspection、revocation 和登出端点，以及当前支持的响应类型、grant、认证方式和 `S256`。签名公钥通过 `/.well-known/jwks.json` 发布。

授权码兑换只有在授权请求包含 `openid` scope 时才签发 ID Token。ID Token 使用 RS256，`aud` 是客户端 ID；授权请求中的 `nonce` 会绑定到授权码并写入 ID Token，客户端必须验证 issuer、audience、签名、有效期和 nonce。ID Token 的有效期跟随 access token 生命周期。

`GET /connect/userinfo` 只接受有效的 Bearer access token，不接受 ID Token 或 refresh token。基础响应包含 `sub`、`preferred_username` 和 `name`；`email`、`phone_number` 等字段受 scope 控制，`claims` 请求可进一步请求已支持的字段。token 无效、已撤销、不是 access token 或对应用户已停用时返回 `401 invalid_token`。

### RP-Initiated Logout

客户端调用 `/connect/endsession` 时，如果提供 `post_logout_redirect_uri`，必须同时提供有效的 `id_token_hint`。Provider 会根据 ID Token 识别客户端，并要求登出回调地址与客户端登记值精确匹配；验证通过后：

1. 撤销当前用户的 token 和 SSO session；
2. 清除 Provider 登录 Cookie；
3. 将可选的 `state` 追加到已验证的登出回调地址并重定向。

没有合法回调地址时，Provider 重定向到 `/`。只提供 `post_logout_redirect_uri` 而没有 `id_token_hint` 会返回 `invalid_request`；不要把任意外部地址当作登出回调。

## 9. 扩展边界

### 已实现的协议能力

- Authorization Code、Client Credentials、Device Authorization、Refresh Token；
- OIDC Discovery、JWKS、ID Token、UserInfo 和 RP-Initiated Logout；
- PKCE `S256`；
- `response_mode=query`、`response_mode=form_post`；
- `client_secret_basic`、`client_secret_post`、`client_secret_jwt`、`private_key_jwt`；
- authorization code 一次性消费、refresh token 轮换和客户端边界校验。

### 尚未实现的扩展

| 扩展 | 未实现的原因/边界 |
|------|----------------|
| PAR | 当前没有保存请求对象的推送端点；授权请求直接进入 `/connect/authorize`。 |
| JAR | 当前不接受签名 `request` JWT，不会以 JWT 替代普通授权参数。 |
| JARM | `form_post` 仍是普通参数表单，不提供签名授权响应 JWT。 |
| DPoP | access token 是 Bearer token，不绑定 DPoP 公钥。 |
| mTLS | 不通过客户端证书完成 token endpoint 认证，也不绑定证书到 token。 |
| 动态注册 | 客户端必须由 Workbench 或受控数据库流程登记。 |
| public client `none` | 当前 token endpoint 设计为机密客户端，需要登记的 secret 或 JWT 认证。 |
| CIBA | 没有 backchannel authentication 请求和用户确认轮询。 |

这些扩展不能通过放宽 `redirect_uri`、关闭 PKCE 或允许认证方式混用来“模拟”。如果目标是 FAPI、金融级开放平台或跨组织第三方生态，应先为请求对象、密钥轮换、sender-constrained token 和一致性测试单独建立设计。

## 10. 安全理由总结

1. 先验证 `client_id` 和精确 `redirect_uri`，避免开放重定向和错误信息泄露。
2. 非法 scope 作为可处理的 `invalid_scope` 返回，避免未经处理的异常变成 500，也避免签发超出客户端权限的 code。
3. 按客户端读取 `require_pkce`，兼容可信旧后端，同时让新客户端默认得到 PKCE 保护。
4. 认证来源互斥，避免攻击者利用“优先 Basic”或“回退表单”的实现差异绕过策略。
5. `invalid_client` 使用 401 和认证 challenge，让标准 OAuth 客户端能够正确区分“重新认证”和“业务参数错误”。
6. `state` 只在安全回跳时保留，既支持客户端防 CSRF，又不为不可信 redirect URI 提供错误投递通道。
7. 将 PAR/JAR/JARM/DPoP/mTLS 等未实现能力明确写入边界，避免接入方误把普通参数或 Bearer token 当成更高强度的安全机制。
