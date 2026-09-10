# WebAuthnNexusAuth 独立部署

`docker-compose.webauthn.yml` 与现有 `docker-compose.yml` 完全隔离：使用独立的 Compose project、`WebAuthnNexusAuth` PostgreSQL 数据库、Docker volume、Provider 容器和本机端口 `5200`。PostgreSQL 仅向本机发布 `56783` 端口，供 DBeaver 等本地数据库工具连接。

本地启动：

```bash
docker compose -f docker-compose.webauthn.yml up --build
```

首次初始化会在全新的 `WebAuthnNexusAuth` 数据库中执行 `webauthn-production-init.sql`，其中包含主 schema 与 `webauthn_credentials`、`webauthn_challenges`。不会连接、修改或读取现有 `nexusauth` 数据库。

DBeaver 新建 PostgreSQL 连接时使用：

| 配置项 | 值 |
| --- | --- |
| Host | `localhost` |
| Port | `56783` |
| Database | `WebAuthnNexusAuth` |
| Username | `webauthnnexusauth` |
| Password | `webauthnnexusauth_dev_password` |
| Schema | `nexusauth` |

若通过环境变量覆盖了 `WEBAUTHN_NEXUSAUTH_POSTGRES_USER` 或 `WEBAUTHN_NEXUSAUTH_POSTGRES_PASSWORD`，DBeaver 也必须使用覆盖后的值。

本地浏览器将访问 `http://localhost:5200`；`localhost` 是 WebAuthn 允许的开发 secure-context 例外。生产必须将下列值统一替换为同一个 HTTPS 域名：

```bash
WEBAUTHN_NEXUSAUTH_ENVIRONMENT=Production
WEBAUTHN_NEXUSAUTH_ENABLED=true
WEBAUTHN_NEXUSAUTH_ISSUER=https://auth.example.com
WEBAUTHN_NEXUSAUTH_RP_ID=auth.example.com
WEBAUTHN_NEXUSAUTH_ORIGIN=https://auth.example.com
WEBAUTHN_NEXUSAUTH_POSTGRES_PASSWORD=REPLACE_WITH_SECRET
```

`WEBAUTHN_NEXUSAUTH_ENABLED` 控制独立 Compose 部署是否开启 Passkey，默认值为 `true`。它会映射到 Provider 的 `NEXUSAUTH_WEBAUTHN_ENABLED`：

- `true`：登录页显示 **Sign in with a passkey**；新用户注册后进入可选择、可跳过的 Passkey 创建页。
- `false`：不显示 Passkey 登录入口，注册后不进入 Passkey 创建页，Passkey handlers 也不可用。

部署在反向代理后时，代理必须将外部 HTTPS Host 和 Proto 正确传给 Provider；RP ID、Origin 和用户访问地址不一致会导致浏览器拒绝创建或使用 Passkey。
