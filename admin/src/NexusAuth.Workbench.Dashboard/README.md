# NexusAuth Workbench Dashboard

Workbench Dashboard 是 NexusAuth 的 React 管理界面。它只通过 Workbench API BFF 完成登录和管理操作，不直接持有 OAuth 客户端密钥、access token 或 refresh token。

## 技术栈

- React 19、TypeScript、Vite 6；
- TDesign React 和 TDesign Icons；
- Axios（统一请求与 API 结果解包）；
- Vite 开发代理和 Nginx 生产静态托管。

## 可用命令

| 命令 | 作用 |
|---|---|
| `npm install` | 安装前端依赖 |
| `npm run dev` | 启动 Vite 开发服务器，默认 `http://localhost:5273` |
| `npm run build` | 执行 `tsc -b` 和 Vite 生产构建 |
| `npm run lint` | 执行 ESLint |
| `npm run preview` | 预览已构建的 `dist` |

项目没有单独的 `npm run typecheck` 脚本；类型检查包含在 `npm run build` 中。

## 本地开发

从仓库根目录先启动 Provider 和 Workbench API：

```bash
dotnet run --project src/NexusAuth.Host
dotnet run --project admin/src/NexusAuth.Workbench.Api
```

再在本目录安装依赖并启动 Dashboard：

```bash
npm install
npm run dev
```

打开 `http://localhost:5273`。`vite.config.ts` 将 `/api` 代理到 `http://localhost:5051`，浏览器因此通过同源路径访问 BFF 并携带 Cookie。API Swagger 地址为 `http://localhost:5051/swagger`。

本地启动前需要先初始化 PostgreSQL 的 `nexusauth` schema，并登记 `nexusauth.workbench` 客户端和 `workbench` API resource。完整数据库和 API 配置见 [Workbench 接入说明](../../../document/06-对接NexusAuth.Workbench.md)。

## 登录流程

1. Dashboard 启动后请求 `GET /api/auth/me` 检查当前 Workbench Cookie。
2. 未登录时，登录页请求 `GET /api/auth/login`；Workbench API 生成 `state`、`nonce` 和 S256 PKCE 参数并返回授权地址。
3. 浏览器跳转 Provider 的 `/connect/authorize`，用户完成登录和授权。
4. Provider 回调 Workbench API 的 `GET /signin-oidc`。API 在服务端使用 `client_secret_basic` 兑换授权码、校验 ID Token 并写入 HttpOnly Cookie。
5. API 重定向 Dashboard 的 `/auth/callback`；前端重新请求 `/api/auth/me`，随后进入 `/dashboard`。
6. 管理请求默认使用 Cookie。若请求带 `Authorization: Bearer ...`，Workbench API 的 policy scheme 会转交 JWT bearer 验证。

API 的 Cookie 名为 `.NexusAuth.Workbench`，默认是独立的 24 小时 session，启用滑动续期。它与 Provider 登录页的“几天内免登录”不等同；Provider 的该期限由 `NEXUSAUTH_LOGIN_FLOW_REMEMBER_ME_LIFETIME_DAYS` 控制。

Workbench API 在每次 Cookie 验证时检查保存的 token：access token 距过期不超过 1 分钟时自动调用 refresh token grant，保存轮换后的 token 并续签新的 24 小时 session；token 无效或刷新失败时清除 Cookie，Dashboard 会在受保护请求收到 401 后回到登录页。

登出调用 `POST /api/auth/logout`。Workbench API 会清除本地 Cookie；若启用 Provider 登出且存在 ID Token，返回 `/connect/endsession` 地址，Dashboard 再跳转该地址完成 Provider 全局登出。

## 请求与 API 结果

`src/api/request.ts` 创建了 `baseURL: '/api'`、`withCredentials: true` 的 Axios 实例：

- `/auth/` 请求的 401 不触发全局跳转，避免登录检查形成死循环；
- 其他受保护请求收到 401 时跳转 `/login`；
- 带 `success/result` 的管理 API 响应会自动解包，失败结果转成 Axios 错误；
- `/api/auth/*` 和 `/signin-oidc` 是认证流程端点，不依赖普通管理 API 的结果包装。

不要在 Dashboard 的源代码、构建变量、localStorage 或 sessionStorage 中保存 `client_secret`、access token 或 refresh token。

## 管理模块

当前可用的 Workbench 管理接口/页面围绕以下资源：

- OAuth 客户端：查询、创建、编辑、删除和凭据生成/重置；
- 用户：查询、资料更新、启停和密码重置；
- API resources：查询、创建、编辑、启停和删除；
- 登录审计：按条件查询登录记录；
- SCIM 凭证：创建、更新和撤销；
- 客户端元数据：读取客户端元数据。

Dashboard 菜单中的其他业务页面属于当前仓库的业务模块或占位模块，不应被当作 NexusAuth Provider 的通用 OAuth 管理能力。

## 生产构建与 Docker

从仓库根目录执行完整构建：

```bash
npm --prefix admin/src/NexusAuth.Workbench.Dashboard run build
npm --prefix admin/src/NexusAuth.Workbench.Dashboard run lint
```

Compose 方式：

```bash
docker compose up --build
```

Dashboard Dockerfile 使用 Node 22 构建前端，再使用 Nginx 1.27 提供静态文件。宿主机端口 `5273` 映射到容器端口 `80`；Nginx 将 `/api` 代理到 `admin-api:8080`，其余路由回退到 `index.html`，因此 React 路由刷新可以正常工作。

生产反向代理必须：

- 将 `/api` 转发到 Workbench API，并保留 `Host`、`X-Forwarded-Proto`、`X-Forwarded-Host` 和 Cookie；
- 使用 HTTPS，确保 Cookie 的 Secure、HttpOnly 和 SameSite 策略符合部署域名；
- 将 Provider 登记的 OIDC 回调设置为 API 地址的 `/signin-oidc`，不要设置成 Dashboard 地址；
- 通过 Workbench API 的环境变量注入客户端密钥，Dashboard 不需要也不应该读取该密钥。

## 相关文档

- [NexusAuth Admin 总览](../../README.md)
- [Workbench API 接入说明](../../../document/06-对接NexusAuth.Workbench.md)
- [高级配置](../../../document/08-高级配置.md)
- [常见问题](../../../document/09-常见问题.md)
- [完整使用手册](../../../document/12-使用手册.md)
