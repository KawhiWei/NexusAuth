# NexusAuth 开放 API

NexusAuth Host 对服务端消费者提供只读目录接口。它与 Workbench 管理 API 分离：Workbench 用于创建和吊销凭据，Host 才是开放接口的调用入口。

## 1. 凭据类型

在 Workbench 调用 `POST /api/open-api-credentials` 创建凭据。明文 `token` 只会在创建响应中出现一次，必须写入 Vault、Kubernetes Secret 或等效的密钥存储。

| `targetType` | 内置 scope | 允许调用的接口 |
| --- | --- | --- |
| `application` | `application:read` | `GET /openapi/v1/applications` |
| `service_resource` | `service_resource:read` | `GET /openapi/v1/service-resources` |

一个凭据只能属于一个目标类型。权限中心同时需要两类目录时，创建两把凭据并分别保存；不要共享，也不要把密钥交给浏览器。

创建请求：

```http
POST /api/open-api-credentials
Content-Type: application/json

{
  "name": "permission-center-application-reader",
  "targetType": "application",
  "expiresAt": "2027-09-01T00:00:00Z"
}
```

响应中的 `token` 只出现一次。`GET /api/open-api-credentials` 只返回摘要，`POST /api/open-api-credentials/{id}/revoke` 会立即使凭据失效。

## 2. Host 接口

所有请求在服务端携带 Bearer token：

```sh
curl 'https://sso.example.com/openapi/v1/applications?keyword=permission' \
  -H 'Authorization: Bearer REPLACE_WITH_APPLICATION_TOKEN'
```

应用目录返回 `id`、`clientId`、`clientName`、`description`、`isActive` 和 `createdAt`，不会返回 OAuth 客户端 secret、回调 URI 或授权配置。

服务资源目录：

```sh
curl 'https://sso.example.com/openapi/v1/service-resources' \
  -H 'Authorization: Bearer REPLACE_WITH_SERVICE_RESOURCE_TOKEN'
```

服务资源目录返回 `id`、`name`、`displayName`、`audience`、`description`、`isActive` 和 `createdAt`。

错误凭据、过期凭据、已吊销凭据或将一类凭据用于另一类端点，都会返回 `401 invalid_token`。
