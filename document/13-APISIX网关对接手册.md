# APISIX Dashboard 手工配置 NexusAuth OIDC 手册

本文只通过 APISIX Dashboard 配置网关，不使用 bootstrap 脚本或自动创建路由。`docker-compose.apisix.yml` 只负责启动 APISIX、etcd 和 APISIX Dashboard。

## 1. 最终访问关系

```text
浏览器 -> http://localhost:9180/api/* -> APISIX -> admin-api:8080
                         |
                         | 未登录时 302
                         v
              http://localhost:5100/connect/authorize
                         |
                         | 登录成功后回调
                         v
              http://localhost:9180/api/callback
```

| 组件 | 地址 | 是否由 APISIX 对外代理 |
| --- | --- | --- |
| NexusAuth SSO | `http://localhost:5100` | 否，浏览器直接访问 |
| Workbench Dashboard | `http://localhost:5560` | 否 |
| APISIX Gateway | `http://localhost:9180` | 是，承载业务 API |
| APISIX Dashboard | `http://127.0.0.1:9000` | 否，仅运维使用 |

## 2. 启动基础容器

项目根目录 `.env` 至少配置 APISIX Admin API 密钥：

```dotenv
APISIX_ADMIN_KEY=replace-with-a-long-random-admin-key
```

首次使用时创建应用和网关之间的共享网络，然后分别启动两套 Compose：

```bash
docker network create nexusauth-gateway
docker compose up -d
docker compose -f docker-compose.apisix.yml up -d
```

如果网络已经存在，`docker network create` 返回已存在即可忽略。

检查状态：

```bash
docker compose ps
docker compose -f docker-compose.apisix.yml ps
```

打开 APISIX Dashboard：`http://127.0.0.1:9000`

- 用户名：`admin`
- 密码：`admin`

这是本地开发账号。生产环境必须修改 `docker/apisix/dashboard.yaml` 中的密码与 JWT secret，并限制管理端口访问来源。

## 3. NexusAuth Client 前置检查

先打开 Workbench：`http://localhost:5560`，进入应用管理，检查已有 Client `nexusauth.apisix.gateway`。

必须满足以下条件：

| 配置项 | 要求 |
| --- | --- |
| Client ID | `nexusauth.apisix.gateway` |
| Client 类型 | Confidential client |
| Client secret | 已创建，并且当前操作者知道明文值 |
| Token endpoint auth method | `client_secret_basic` |
| Grant types | `authorization_code`、`refresh_token` |
| PKCE | 启用，S256 |
| Redirect URI | `http://localhost:9180/api/callback` |
| Post logout redirect URI | `http://localhost:9180/` |
| Identity scopes | `openid profile offline_access` |
| API scope | `nexusauth.workbench.api`，或目标服务的 audience |
| 状态 | Active |

Client secret 只填写到 APISIX Dashboard 的 OIDC 配置中，不写入前端代码或文档。

如果以后保护订单服务，应先在 NexusAuth 创建 audience 为 `orders-api` 的服务资源，并把该资源绑定到这个 Client。APISIX 中填写 scope 不会自动创建 NexusAuth 授权关系。

## 4. 创建内部 SSO Backchannel Route

这一步只为本地无域名环境服务。

NexusAuth discovery 的 issuer 和端点是 `http://localhost:5100`。浏览器访问这个地址时直接进入 SSO 容器；但 APISIX 容器访问 `localhost:5100` 时，localhost 指向 APISIX 自己。因此 APISIX 配置为在容器内部监听 `5100`，需要一条仅匹配该端口的回源路由。

在 APISIX Dashboard 进入 **Routes -> Create**，填写：

| 字段 | 值 |
| --- | --- |
| Route ID | `nexusauth-sso-backchannel` |
| Name | `NexusAuth SSO Backchannel` |
| URI | `/*` |
| Priority | `100` |
| Status | Enabled |
| Plugin | 不添加任何认证插件 |

在高级匹配条件中增加：

| Variable | Operator | Value |
| --- | --- | --- |
| `server_port` | `==` | `5100` |

配置 Upstream：

| 字段 | 值 |
| --- | --- |
| Type | `roundrobin` |
| Scheme | `http` |
| Node host | `sso` |
| Node port | `8080` |
| Weight | `1` |

保存后，这条 Route 的核心配置应等价于：

```json
{
  "id": "nexusauth-sso-backchannel",
  "name": "NexusAuth SSO Backchannel",
  "uri": "/*",
  "vars": [["server_port", "==", "5100"]],
  "priority": 100,
  "upstream": {
    "type": "roundrobin",
    "nodes": { "sso:8080": 1 }
  }
}
```

不要使用 `hosts: ["localhost"]` 代替端口条件，否则浏览器访问 `localhost:9180/api/*` 时也可能命中这条 Route。

## 5. 创建可复用 OIDC Plugin Config

参考资料：

- [APISIX openid-connect 官方文档](https://apisix.apache.org/docs/apisix/plugins/openid-connect/)
- 本项目当前容器版本：APISIX `3.11.0`

### 5.1 必填字段清单

官方 schema 的硬性必填字段只有三个；但完成 NexusAuth 浏览器登录还需要一组场景必填字段：

| 字段 | 官方 schema 必填 | 本方案是否必须显式填写 | 本方案值 | 原因 |
| --- | --- | --- | --- | --- |
| `client_id` | 是 | 是 | `nexusauth.apisix.gateway` | 标识 NexusAuth Client |
| `client_secret` | 是 | 是 | Client 的真实 secret | APISIX 在 token endpoint 认证自身 |
| `discovery` | 是 | 是 | `http://sso:8080/.well-known/openid-configuration` | 获取授权、token、userinfo 和 JWKS 端点 |
| `session.secret` | 浏览器模式需要 | 是 | 32 字节以上随机值 | 加密并签名 APISIX 浏览器会话；至少 16 字符 |
| `redirect_uri` | 否 | 是 | `http://localhost:9180/api/callback` | 必须与 NexusAuth Client 登记值完全一致 |
| `scope` | 否，默认 `openid` | 是 | `openid profile offline_access nexusauth.workbench.api` | 申请身份信息、刷新令牌和目标 API audience |
| `use_pkce` | 否，默认 `false` | 是 | `true` | NexusAuth Client 已要求 PKCE/S256 |
| `bearer_only` | 否，默认 `false` | 是，建议显式填写 | `false` | 允许未登录浏览器跳转 SSO，而不是只返回 401 |
| `unauth_action` | 否，默认 `auth` | 是，建议显式填写 | `auth` | 未认证时执行授权跳转 |
| `token_endpoint_auth_method` | 否 | 是，建议显式填写 | `client_secret_basic` | 与 NexusAuth Client 认证方式保持一致 |
| `ssl_verify` | 否，默认 `false` | 本地是 | `false` | 当前 SSO 是 HTTP；生产必须使用 HTTPS 并设为 `true` |
| `set_access_token_header` | 否，默认 `true` | 是，建议显式填写 | `true` | 把 access token 交给上游 API |
| `access_token_in_authorization_header` | 否，默认 `false` | 是 | `true` | 上游通过标准 `Authorization: Bearer` 验证 JWT |

结论：如果只看 APISIX schema，必填是 `client_id`、`client_secret`、`discovery`；如果要让当前 NexusAuth 登录链路真正可用，请把表中“本方案必须”的字段全部填写。

### 5.2 Dashboard 表单填写

进入 **Plugin Configs -> Create**：

| 字段 | 值 |
| --- | --- |
| ID | `nexus-oidc-workbench` |
| Description | `NexusAuth OIDC for Workbench API` |

添加并启用 `openid-connect` 插件，逐项填写：

| OIDC 字段 | 值 | 说明 |
| --- | --- | --- |
| `client_id` | `nexusauth.apisix.gateway` | 已有 NexusAuth Client |
| `client_secret` | Client 的真实 secret | 必须与 NexusAuth 中保存的凭据一致 |
| `discovery` | `http://sso:8080/.well-known/openid-configuration` | APISIX 容器访问地址 |
| `redirect_uri` | `http://localhost:9180/api/callback` | 必须与 Client 登记值完全一致 |
| `scope` | `openid profile offline_access nexusauth.workbench.api` | 按目标 API audience 调整 |
| `use_pkce` | `true` | NexusAuth Client 要求 PKCE |
| `bearer_only` | `false` | 未登录浏览器需要跳转登录页 |
| `unauth_action` | `auth` | 未登录时启动 OIDC 授权流程 |
| `token_endpoint_auth_method` | `client_secret_basic` | 与 Client 配置一致 |
| `ssl_verify` | `false` | 仅限当前 HTTP 本地开发环境 |
| `set_access_token_header` | `true` | 将 access token 传给上游 |
| `access_token_in_authorization_header` | `true` | 使用 `Authorization: Bearer` |
| `set_id_token_header` | `false` | 上游通常不需要 ID token |
| `set_userinfo_header` | `true` | 需要时传递用户信息 |
| `session.secret` | 独立的 32 字节以上随机值 | 不要与 Client secret 相同 |

可用以下命令只生成 session secret，然后手工填入 Dashboard：

```bash
openssl rand -base64 48
```

APISIX Dashboard 3.0.1 的表单可能不显示所有高级字段。至少必须保存 `client_id`、`client_secret`、`discovery`、`redirect_uri`、`scope`、`use_pkce=true`、`bearer_only=false`、`session.secret`。如果 `unauth_action` 没有表单项，`bearer_only=false` 下默认行为即为浏览器认证跳转；保存后必须通过实际 302 验证。

### 5.3 完整 JSON 模板

下面的 JSON 保留为核对清单。将两个 `REPLACE_...` 替换成真实值；不要把替换后的 secret 提交到 Git。

```json
{
  "id": "nexus-oidc-workbench",
  "desc": "NexusAuth OIDC for Workbench API",
  "plugins": {
    "openid-connect": {
      "client_id": "nexusauth.apisix.gateway",
      "client_secret": "REPLACE_WITH_NEXUSAUTH_CLIENT_SECRET",
      "discovery": "http://sso:8080/.well-known/openid-configuration",
      "redirect_uri": "http://localhost:9180/api/callback",
      "scope": "openid profile offline_access nexusauth.workbench.api",
      "use_pkce": true,
      "bearer_only": false,
      "unauth_action": "auth",
      "token_endpoint_auth_method": "client_secret_basic",
      "ssl_verify": false,
      "set_access_token_header": true,
      "access_token_in_authorization_header": true,
      "set_id_token_header": false,
      "set_userinfo_header": true,
      "set_refresh_token_header": false,
      "session": {
        "secret": "REPLACE_WITH_RANDOM_SESSION_SECRET_MIN_32_CHARS"
      }
    }
  }
}
```

Dashboard 以表单方式保存时，最终返回的 JSON 可能自动补充默认字段，这是正常现象。判断是否正确时，以 `client_id`、discovery、redirect URI、scope、PKCE、会话 secret 和未登录跳转行为为准。

“插件系统”页面表示 APISIX 安装了哪些插件以及全局规则状态。创建 Plugin Config 不会把 `openid-connect` 标记为全局启用，这是正常状态；实际生效由 Route 引用该 Plugin Config 决定。

## 6. 创建业务 API Route

进入 **Routes -> Create**：

| 字段 | 值 |
| --- | --- |
| Route ID | `nexus-api-gateway` |
| Name | `Nexus API Gateway` |
| URI | `/api/*` |
| Priority | `200` |
| Status | Enabled |
| Plugin Config | `nexus-oidc-workbench` |

配置 Upstream：

| 字段 | 值 |
| --- | --- |
| Type | `roundrobin` |
| Scheme | `http` |
| Node host | `admin-api` |
| Node port | `8080` |
| Weight | `1` |

Route 上不要再次添加一份 `openid-connect`，否则会出现两套配置来源。Route 只引用 `plugin_config_id=nexus-oidc-workbench`。

保存后的核心结构应等价于：

```json
{
  "id": "nexus-api-gateway",
  "name": "Nexus API Gateway",
  "uri": "/api/*",
  "priority": 200,
  "plugin_config_id": "nexus-oidc-workbench",
  "upstream": {
    "type": "roundrobin",
    "nodes": { "admin-api:8080": 1 }
  }
}
```

## 7. 验证登录流程

先清除 `localhost:9180` 的旧 Cookie或使用浏览器隐私窗口，然后访问：

```text
http://localhost:9180/api/auth/me
```

正确流程：

1. APISIX 判断没有有效会话。
2. APISIX 返回 `302`。
3. 浏览器跳转到 `http://localhost:5100/connect/authorize`。
4. URL 中包含 `client_id=nexusauth.apisix.gateway`。
5. URL 中包含 `code_challenge` 和 `code_challenge_method=S256`。
6. 登录 NexusAuth。
7. NexusAuth 回调 `http://localhost:9180/api/callback`。
8. APISIX 使用授权码和 PKCE verifier 换取 token，并写入 session Cookie。
9. APISIX 恢复原始 API 请求并转发到 `admin-api:8080`。

只验证第一段跳转可以执行：

```bash
curl -I http://localhost:9180/api/auth/me
```

预期是 `HTTP/1.1 302`，`Location` 指向 `http://localhost:5100/connect/authorize`。

## 8. 前端跨端口访问

Workbench 页面在 `http://localhost:5560`，网关在 `http://localhost:9180`。前端请求必须携带 Cookie：

```ts
const api = axios.create({
  baseURL: "http://localhost:9180/api",
  withCredentials: true,
});
```

如果浏览器报告 CORS 错误，在 `nexus-oidc-workbench` Plugin Config 中再添加 `cors`：

| 字段 | 建议值 |
| --- | --- |
| `allow_origins` | `http://localhost:5560` |
| `allow_methods` | `GET,POST,PUT,PATCH,DELETE,OPTIONS` |
| `allow_headers` | `Content-Type,Authorization` |
| `allow_credential` | `true` |

启用凭据时不能使用 `allow_origins=*`。

## 9. 新增其他服务

同一个 audience 的 Route 可以复用 `nexus-oidc-workbench`。如果新服务使用不同 audience，例如 `orders-api`：

1. 在 NexusAuth 创建并启用 `orders-api` 服务资源。
2. 将 `orders-api` 绑定给 `nexusauth.apisix.gateway` Client。
3. 创建新的 Plugin Config，例如 `nexus-oidc-orders`。
4. scope 填 `openid profile offline_access orders-api`。
5. 创建 `/orders/*` Route，引用 `nexus-oidc-orders`。
6. Upstream 指向订单服务容器名和端口。

不要让一条业务 Route 同时申请多个无关 audience。后端还应验证 JWT 的签名、issuer、自己的 audience、过期时间和 `token_use=access_token`。

## 10. 常见故障

| 现象 | 原因与处理 |
| --- | --- |
| Dashboard 插件系统显示 OIDC“未启用” | 它没有作为 Global Rule 启用。检查 Plugin Config 是否存在、Route 是否引用它。 |
| 请求返回 404 | 检查业务 Route URI、状态、Upstream；确认 backchannel Route 只匹配端口 5100。 |
| 没有跳转登录页 | 检查 `bearer_only=false`、Plugin Config 已被 Route 引用，并清除旧 session Cookie。 |
| 授权地址没有 `code_challenge` | `use_pkce` 未启用。 |
| `invalid_redirect_uri` | NexusAuth Client 与插件的回调地址不完全一致。 |
| `invalid_client` | Client ID 或 secret 错误，或认证方式不是 `client_secret_basic`。 |
| `invalid_scope` | Client 未允许该 scope，或没有绑定对应 API resource。 |
| 回调后 500 / 无法换 token | 检查 `nexusauth-sso-backchannel`、`server_port == 5100` 和 `sso:8080` 网络连通性。 |
| 上游仍返回 401 | 设置 `access_token_in_authorization_header=true`，并检查后端 audience 与 issuer 校验。 |
| 浏览器 CORS 错误 | 添加精确 origin 的 `cors` 插件并允许 credentials。 |

查看运行日志：

```bash
docker compose -f docker-compose.apisix.yml logs --tail=200 apisix
docker compose -f docker-compose.apisix.yml logs --tail=200 apisix-dashboard
```

## 11. 生产环境调整

- 使用真实 HTTPS 域名，不再使用本地 `5100` backchannel Route。
- issuer、discovery 中的端点和 redirect URI 必须使用浏览器与 APISIX 都能访问的 HTTPS 地址。
- `ssl_verify=true`，session Cookie 使用 Secure。
- APISIX Dashboard 和 Admin API 不对公网开放。
- Client secret、session secret、Admin key 使用 Vault、Kubernetes Secret 或等价密钥管理服务。
- 修改默认 Dashboard 账号密码，并限制管理网络来源。
