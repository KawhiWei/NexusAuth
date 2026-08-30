# NexusAuth User Guide

> Applies to the current repository version. This guide is the primary reference for administrators, application integrators, and operators. See the related documents at the end for protocol details and source-level design notes.

## 1. Product Scope and Boundaries

NexusAuth is a .NET 10 OAuth 2.0 and OpenID Connect Provider with a Provider service, Workbench management API, Workbench Dashboard, and PostgreSQL database. It provides centralized sign-in, authorization, token issuance, user management, client management, API resource management, and SCIM 2.0 user provisioning.

It is currently intended for confidential clients, web/BFF applications, service-to-service calls, and constrained devices. Browser SPAs, mobile apps, and installed desktop apps cannot safely keep a client secret and must integrate through a BFF. The following extensions are not implemented: PAR, JAR, JARM, DPoP, mTLS-bound tokens, dynamic client registration, CIBA, and `token_endpoint_auth_method=none`.

| Service | Default address | Responsibility |
|---|---|---|
| Provider | `http://localhost:5100` | Sign-in, authorization, tokens, OIDC, and SCIM |
| Workbench API | `http://localhost:5051` | Management BFF and administration API |
| Workbench Dashboard | `http://localhost:5273` | Administration UI |
| PostgreSQL | `localhost:5432` | Application data |

## 2. Quick Start

### 2.1 Prerequisites

- Docker Engine and Docker Compose; or .NET 10, Node.js 20+, npm, and PostgreSQL 16.
- Local ports `5100`, `5051`, `5273`, and `5432` must be available.
- Production requires HTTPS, managed secrets, and a signing certificate. Do not use local development fallback values in production.

### 2.2 Compose Development Environment

From the repository root, run:

```bash
docker compose up --build
```

On the first startup, Compose creates the final schema in an empty database, registers the Workbench system client, and creates the bootstrap administrator from environment variables. Open `http://localhost:5273` and sign in with the Compose development fallback account: username `admin`, password `wzw0126..`.

This account and the default Workbench secret are for local development only. They are not production credentials and must never be used in production. Stop services while preserving the data volume with:

```bash
docker compose down
```

Reset the database only when local data may be discarded:

```bash
docker compose down -v
docker compose up --build
```

### 2.3 Local Process Startup

Prepare the database and schema as described in [Database Configuration](../../document/03-数据库配置.md), then run each service:

```bash
dotnet run --project src/NexusAuth.Host
dotnet run --project admin/src/NexusAuth.Workbench.Api
cd admin/src/NexusAuth.Workbench.Dashboard
npm install
npm run dev
```

Starting services directly does not provision a production administrator or Workbench secret automatically. Configure the variables in section 8 in the runtime environment.

## 3. Bootstrap Administrator and Sign-In

The initial administrator is created from the Provider `BootstrapAdmin` configuration, not from a fixed SQL account. Set the username and password together:

```bash
NEXUSAUTH_BOOTSTRAP_ADMIN_USERNAME=admin
NEXUSAUTH_BOOTSTRAP_ADMIN_PASSWORD=REPLACE_WITH_A_STRONG_PASSWORD
NEXUSAUTH_BOOTSTRAP_ADMIN_NICKNAME="System Admin"
NEXUSAUTH_BOOTSTRAP_ADMIN_EMAIL=admin@example.com
```

Bootstrap is idempotent: if the username does not exist, NexusAuth creates it and marks it as a system account. If it already exists, its password is retained and the account remains a system account. System accounts cannot be changed or deleted through management functions; manage them through controlled startup configuration and database recovery procedures.

After Dashboard sign-in, the browser holds only the protected Workbench BFF cookie. OAuth client secrets, access tokens, and refresh tokens must not be placed in browser storage or frontend build artifacts.

## 4. Administration Console

The console contains users, API resources, applications, and SCIM credentials. Resource names, client IDs, callback URIs, and credentials are security boundaries. Confirm the dependent applications before changing them.

### 4.1 User Management

Use **User Management** to search users, edit display information, enable or disable accounts, and reset passwords. The management UI presents username, nickname, email, phone number, status, and SCIM profile fields as business information. The internal user ID is for APIs and audit correlation only, not routine administration.

- Disabling a user blocks future sign-ins. Re-enable the account before it can authenticate again.
- After a password reset, notify the user through a secure channel to set a new password.
- System accounts cannot be edited, disabled, have passwords reset, or be deleted.
- SCIM-provisioned users are visible here. Use `externalId` for an upstream system key; do not use the local UUID as an external identity-provider ID.

### 4.2 API Resources and Scopes

An API resource represents an API that an access token may authorize. Supply the following fields when creating one:

| Field | Rule |
|---|---|
| `name` | Scope name, unique across the entire system, such as `orders.read`; it cannot be renamed after creation. |
| `displayName` | Display name in the management console. |
| `audience` | Intended audience for the access token, such as `orders-api`. |
| `description` | Optional business description. |
| Status | Once disabled, the scope must not be issued in newly authorized requests. |

The standard scopes are `openid`, `profile`, `email`, `phone`, `address`, and `offline_access`. `openid` enables OIDC; `profile` and related scopes request user claims; `offline_access` requests a refresh token. Do not recreate these as ordinary API resources. Create business API scopes as API resources instead.

Before deleting an API resource, remove it from every client. Deletion affects scope validation for new authorization requests. Names cannot be duplicated; a different audience does not permit the same name.

### 4.3 Application (OAuth Client) Management

Configure the following when creating an application:

| Setting | Recommendation and constraint |
|---|---|
| `client_id` | Unique and stable, for example `orders-bff`; do not change it after creation. |
| Callback URIs | `redirect_uri` and `post_logout_redirect_uri` must match exactly, including scheme, host, port, path, and case. |
| Grant types | Select only the required flows: `authorization_code`, `refresh_token`, `client_credentials`, or `device_code`. |
| PKCE | Recommended for web/BFF clients; only `S256` is accepted. |
| Client authentication | Select one method: `client_secret_basic`, `client_secret_post`, `client_secret_jwt`, or `private_key_jwt`. |
| Standard scopes | Select standard scopes such as `openid profile offline_access`. |
| API resources | Select one or more enabled business resources. The backend combines standard scopes and resource names, removes duplicates, and writes them to the client's `AllowedScopes`. |

Recommended BFF configuration: `authorization_code` plus `refresh_token`, `require_pkce=true`, `client_secret_basic`, and scopes `openid profile offline_access YOUR_API_SCOPE`. Use `client_credentials` for machine-to-machine calls; do not request `openid` or user-profile scopes for this flow.

After generating or resetting a credential, the plaintext `client_secret` or private key is shown only once. Store it immediately in Vault, a Kubernetes Secret, or another managed secret store. Never put it in Git, frontend environment variables, logs, or tickets. Resetting a credential invalidates the old shared secret; update dependent applications first and prepare a rollback plan.

### 4.4 Management API

The Dashboard uses the following OIDC-protected APIs. Every `id` is an internal UUID. Prefer the Dashboard for routine operations; the APIs are intended for automation and troubleshooting.

| Object | API |
|---|---|
| Current sign-in, sign-in, sign-out | `GET /api/auth/me`, `GET /api/auth/login`, `POST /api/auth/logout` |
| Users | `GET /api/users`, `GET/PUT /api/users/{id}`, `PATCH /api/users/{id}/status`, `POST /api/users/{id}/reset-password` |
| OAuth clients | `GET/POST /api/clients`, `PUT/DELETE /api/clients/{id}`, `POST /api/clients/{id}/credentials`, `POST /api/clients/{id}/credentials/reset` |
| API resources | `GET/POST /api/api-resources`, `PUT/DELETE /api/api-resources/{id}`, `PATCH /api/api-resources/{id}/status` |
| Client metadata | `GET /api/client-metadata` |
| SCIM credentials | `GET/POST /api/scim-credentials`, `PUT /api/scim-credentials/{id}`, `POST /api/scim-credentials/{id}/revoke` |

## 5. OAuth 2.0 and OIDC Integration

Read the discovery document instead of hard-coding endpoints:

```bash
curl http://localhost:5100/.well-known/openid-configuration
```

The primary endpoints are `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/endsession`, `/connect/deviceauthorization`, `/connect/introspect`, `/connect/revocation`, and `/.well-known/jwks.json`.

### 5.1 Authorization Code with PKCE

1. Register a confidential BFF client and enable `authorization_code` and PKCE.
2. For every sign-in, generate a random `state`, OIDC `nonce`, and a 43-to-128-character `code_verifier`; calculate Base64Url(SHA-256(verifier)) as `code_challenge`.
3. Redirect the browser to `/connect/authorize`; the callback URI must exactly match the registered value.
4. The BFF validates the callback `state`, then exchanges the authorization code at `/connect/token` from the server.
5. Store tokens in a protected server-side session or HttpOnly cookie. React and other frontend code calls only the BFF.

Example authorization request:

```text
http://localhost:5100/connect/authorize?response_type=code&client_id=orders-bff&redirect_uri=https%3A%2F%2Fapp.example.com%2Fsignin-oidc&scope=openid%20profile%20offline_access%20orders.read&state=RANDOM_STATE&nonce=RANDOM_NONCE&code_challenge=S256_CHALLENGE&code_challenge_method=S256
```

Exchange with `client_secret_basic`:

```bash
curl -i -X POST http://localhost:5100/connect/token \
  -u 'orders-bff:REPLACE_WITH_CLIENT_SECRET' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=authorization_code' \
  --data-urlencode 'client_id=orders-bff' \
  --data-urlencode 'code=REPLACE_WITH_AUTHORIZATION_CODE' \
  --data-urlencode 'redirect_uri=https://app.example.com/signin-oidc' \
  --data-urlencode 'code_verifier=ORIGINAL_VERIFIER'
```

`response_mode` supports `query` and `form_post`. The Provider returns an error and the original `state` only after it has safely validated both the client and `redirect_uri`; otherwise it renders a local Provider error page.

### 5.2 Service-to-Service Calls

Set the client grant type to `client_credentials` and configure only business API scopes. Request a token using the registered authentication method:

```bash
curl -X POST http://localhost:5100/connect/token \
  -u 'inventory-worker:REPLACE_WITH_CLIENT_SECRET' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'grant_type=client_credentials' \
  --data-urlencode 'scope=inventory.read'
```

This token does not represent an end user. The resource server must validate the signature, `iss`, expiry, `aud`, and required scopes.

### 5.3 Refresh, Sign-Out, and Revocation

A response includes a refresh token only when the client permits `refresh_token` and the authorization request includes `offline_access`. Refreshing rotates the token, so clients must replace the stored value atomically; concurrent reuse of the old token fails. Use `/connect/endsession` for user sign-out and `/connect/revocation` for revocation. Never write token contents to logs.

Do not mix client-authentication sources. A conflict among Basic authentication, form credentials, shared-secret JWT assertions, and private-key JWT assertions returns `invalid_client` (HTTP 401 with `WWW-Authenticate`). See [OAuth Client Configuration](../../document/05-配置OAuth客户端.md) for detailed request examples.

## 6. SCIM 2.0 User Provisioning

The SCIM base URL is `https://sso.example.com/scim/v2`. It uses a dedicated Bearer token, not a user access token. Create credentials in **SCIM Credentials** in Workbench. The token is returned only once in the create response; after it is stored, revoke and create a new credential to rotate it.

Create a separate credential for every identity source and grant only the minimum required permission:

| Scope | Permission |
|---|---|
| `scim:read` | Read ServiceProviderConfig, Schema, ResourceType, and users. |
| `scim:write` | Create, replace, PATCH, and delete users. Grant `scim:read` as well for read operations. |

Supported endpoints are `GET /ServiceProviderConfig`, `/ResourceTypes`, `/Schemas`, `GET/POST /Users`, and `GET/PUT/PATCH/DELETE /Users/{id}`. Requests and responses use `application/scim+json` and support `If-Match` ETag concurrency control. `externalId` is the external user key; `userName` is required and unique system-wide.

Example user creation:

```bash
curl -X POST https://sso.example.com/scim/v2/Users \
  -H 'Authorization: Bearer REPLACE_WITH_SCIM_TOKEN' \
  -H 'Content-Type: application/scim+json' \
  --data '{"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"userName":"alice","externalId":"idp-1001","active":true,"name":{"givenName":"Alice","familyName":"Zhang"},"emails":[{"value":"alice@example.com","primary":true}]}'
```

Current limitations: only the User resource is supported; SCIM Bulk, sorting, and Change Password are not supported; `emails` and `phoneNumbers` accept only one primary value each; filter support is defined by the returned `ServiceProviderConfig`. When the upstream IdP can disable instead of delete, prefer `PATCH active=false` to preserve audit history and allow account recovery.

## 7. Production Release Checklist

1. Set the public HTTPS `NEXUSAUTH_ISSUER` and ensure the Provider, discovery document, client callbacks, and reverse proxy agree.
2. Explicitly configure the bootstrap-administrator variables and `WORKBENCH_CLIENT_SECRET`; do not use Compose fallback values.
3. Provide a managed X.509 PFX: set `NEXUSAUTH_SSO_ENVIRONMENT=Production`, `NEXUSAUTH_SIGNING_MODE=Certificate`, `NEXUSAUTH_SIGNING_CERTIFICATE_PATH`, and the password secret. Production does not generate a signing certificate automatically.
4. Share a Data Protection key ring across Provider and Workbench replicas, and persist PostgreSQL data.
5. For an empty database, run [production-init.sql](../../production-init.sql). Upgrade an existing database only by applying [database migrations](../../database) `001_*.sql` through `005_*.sql` in order; do not rerun the final initialization script.
6. Confirm the database account can use `pgcrypto`. When managed PostgreSQL prohibits `CREATE EXTENSION`, ask the DBA to enable it beforehand.
7. Supply `WORKBENCH_CLIENT_SECRET` to both database initialization and Workbench API. At API startup, NexusAuth checks and synchronizes the hash for this built-in client. When rotating the secret, update the secret first and then roll the Workbench deployment.
8. Configure trusted forwarded headers, explicit `AllowedHosts`, HTTPS cookies, log retention, monitoring, backups, and recovery drills.
9. Do not run `demo/seed.sql` in production, and never commit real secrets, PFX files, private keys, tokens, or production database exports.

## 8. Configuration Reference

| Variable | Description |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string for both Provider and Workbench API. |
| `NEXUSAUTH_ISSUER` / `Jwt__Issuer` | Public Provider URL; must use HTTPS in production. |
| `NEXUSAUTH_BOOTSTRAP_ADMIN_*` | Bootstrap system-administrator profile. |
| `WORKBENCH_CLIENT_SECRET` | Sole secret source for the `nexusauth.workbench` system client. |
| `NEXUSAUTH_SSO_ENVIRONMENT` | Set to `Production` in production. |
| `NEXUSAUTH_SIGNING_MODE` | `Certificate` is recommended for new deployments. |
| `NEXUSAUTH_SIGNING_CERTIFICATE_PATH` / `...PASSWORD` | Production PFX path and password. |
| `NEXUSAUTH_ACCESS_TOKEN_LIFETIME_MINUTES` | Access-token lifetime; defaults to 60 minutes. |
| `NEXUSAUTH_REFRESH_TOKEN_LIFETIME_MINUTES` | Absolute refresh-token lifetime; defaults to 43200 minutes. |

## 9. Troubleshooting

| Symptom | Check first |
|---|---|
| `invalid_client` | Client status, authentication method, secret rotation, and whether Basic/form/JWT methods are mixed. For Workbench, also check `WORKBENCH_CLIENT_SECRET`. |
| `redirect_uri` error | The registered and requested values must match character-for-character, including trailing `/`, port, and case. |
| `invalid_scope` | The scope is allowed for the client, the API resource is enabled, and standard-scope and resource names are correct. |
| `invalid_grant` | Authorization code reuse or expiry, callback URI, and PKCE verifier. |
| Sign-in redirect loop | Workbench Authority, callback URI, Cookie/Data Protection settings, and reverse-proxy HTTPS headers. |
| SCIM 401/403 | Bearer token validity, revocation, and expiry; grant `scim:read` for reads and `scim:write` for writes. |
| Provider startup failure | Database connectivity, schema, signing certificate path/password, and production environment variables. |

View logs with `docker compose logs` or in `logs/sso` and `logs/workbench`. Use TraceId to correlate requests while troubleshooting, but never log passwords, client secrets, private keys, authorization codes, access tokens, refresh tokens, or complete cookies.

## 10. Related Documents

- [Quick Start](../../document/01-快速开始.md), [Environment Preparation](../../document/02-环境准备.md), [Database Configuration](../../document/03-数据库配置.md)
- [Starting the Provider](../../document/04-启动NexusAuth.Provider.md), [OAuth Client Configuration](../../document/05-配置OAuth客户端.md)
- [Workbench API Integration](../../document/06-对接NexusAuth.Workbench.md), [Dashboard](../../document/07-对接NexusAuth.Workbench.Dashboard.md)
- [Advanced Configuration](../../document/08-高级配置.md), [FAQ](../../document/09-常见问题.md)
- [Demo](../../document/10-Demo示例详解.md), [OAuth/OIDC Protocol Design](../../document/11-OAuth-OIDC协议设计.md)
- Chinese edition: [NexusAuth User Guide](../../document/12-使用手册.md)
