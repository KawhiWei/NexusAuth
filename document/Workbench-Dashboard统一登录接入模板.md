# Workbench Dashboard：浏览器统一登录模板

这个模板展示 React 前端如何与已接入 NexusAuth 的服务端配合。Dashboard 自身不是 OAuth 客户端：不兑换授权码，不验证 ID Token，也不保存客户端密钥、access token 或 refresh token。对应代码在 [`admin/src/NexusAuth.Workbench.Dashboard`](../admin/src/NexusAuth.Workbench.Dashboard)；后端步骤见[Workbench API 模板](./Workbench-API统一登录接入模板.md)。

## 页面如何进入登录状态

1. 前端先请求同源 `GET /api/auth/me`。已登录时读取最小用户信息；收到 401 就显示 `/login`。
2. 用户点击“使用 NexusAuth 登录”，前端请求 `GET /api/auth/login`，取得服务端生成的 `authorizeUrl`，再用 `window.location.assign` 跳转。`state`、`nonce` 和 PKCE verifier 由 API 保存，前端不自行拼接敏感参数。
3. NexusAuth 把浏览器重定向到 API 的 `/signin-oidc`。API 兑换、验证令牌并设置 HttpOnly Cookie，随后跳回前端 `/auth/callback`。
4. 前端重新请求 `/api/auth/me`，成功后进入受保护页面。普通管理请求使用同源 Cookie；受保护 API 返回 401 时回到登录页。
5. 登出时调用 `POST /api/auth/logout` 清 API 会话；若响应包含 `logoutUrl`，浏览器继续跳到 NexusAuth 的 `/connect/endsession`，完成 Provider 侧登出。

接口由 [`src/api/login.ts`](../admin/src/NexusAuth.Workbench.Dashboard/src/api/login.ts) 封装；路由和登录状态判断见 [`src/router/auth.tsx`](../admin/src/NexusAuth.Workbench.Dashboard/src/router/auth.tsx) 与 [`src/router/routes.tsx`](../admin/src/NexusAuth.Workbench.Dashboard/src/router/routes.tsx)。`/auth/callback` 是前端落地页，不是 NexusAuth 登记的 OAuth 回调地址。不要把客户端的 `redirect_uri` 指向它。

## 同源部署和 Cookie

浏览器通过 `/api` 访问后端，请求开启 `withCredentials`。开发环境的 Vite 代理将 `/api` 与 `/signin-oidc` 转发到本地 API；根目录 Compose 的 Nginx 同样代理这两个路径，因此对外回调为 `http://localhost:5560/signin-oidc`，真正处理回调的是 Workbench API。直接运行 Dashboard 时默认 Vite 端口为 `5273`，Compose 为 `5560`。配置的 `redirect_uri` 必须与公开路径逐字符一致。

前端不从查询串、localStorage 或构建变量里读取令牌；仅依赖 API 签发的 HttpOnly Cookie。应用请求收到 401 时可清理页面登录状态并回到登录入口，但不要对 `/api/auth/me` 的 401 反复跳转，否则会形成登录循环。复制这个模板到其他应用时，后端必须先实现对应的 `/api/auth/*`、回调、Cookie 验证与刷新逻辑；单独复制 React 页面无法完成 OIDC 接入。

## 直接对照

- [`src/pages/login/index.tsx`](../admin/src/NexusAuth.Workbench.Dashboard/src/pages/login/index.tsx)：发起登录。
- [`src/api/login.ts`](../admin/src/NexusAuth.Workbench.Dashboard/src/api/login.ts)：登录状态、退出请求。
- [`src/api/request.ts`](../admin/src/NexusAuth.Workbench.Dashboard/src/api/request.ts)：同源 Cookie 请求和 401 处理。
- [`nginx.conf`](../admin/src/NexusAuth.Workbench.Dashboard/nginx.conf) 与 [`vite.config.ts`](../admin/src/NexusAuth.Workbench.Dashboard/vite.config.ts)：`/api`、`/signin-oidc` 反向代理。
- [Dashboard 项目 README](../admin/src/NexusAuth.Workbench.Dashboard/README.md)：运行与构建命令。

网关模式会关闭 Workbench API 的登录端点，而这份 Dashboard 模板仍调用它们；接入网关时需要另行实现前端入口和登录状态读取，不能直接切换一个后端开关。
