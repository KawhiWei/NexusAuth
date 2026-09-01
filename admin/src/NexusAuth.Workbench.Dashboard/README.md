# NexusAuth Workbench Dashboard

Workbench Dashboard 是 NexusAuth 的 React 管理界面。它通过 Workbench API BFF 完成登录和管理操作，不直接持有 OAuth 客户端密钥或令牌。

## 开发运行

先启动 Provider 和 Workbench API：

```bash
dotnet run --project src/NexusAuth.Host
dotnet run --project admin/src/NexusAuth.Workbench.Api
```

再在本目录安装依赖并启动 Vite：

```bash
npm install
npm run dev
```

默认访问地址为 `http://localhost:5273`。`vite.config.ts` 会把 `/api` 代理到 `http://localhost:5051`，因此本地浏览器请求会携带 Workbench API 的 Cookie。

## 登录流程

1. 页面调用 `GET /api/auth/me` 检查当前 Workbench 会话。
2. 未登录时调用 `GET /api/auth/login`，再将浏览器跳转到 BFF 返回的授权 URL。
3. Provider 登录成功后回调 Workbench API 的 `/signin-oidc`；BFF 在服务端兑换令牌并设置 HttpOnly Cookie。
4. BFF 回到 Dashboard 的 `/auth/callback`，前端重新读取当前用户。

不要在前端代码、Vite 环境变量或浏览器存储中保存 `client_secret`、access token 或 refresh token。

## 构建与部署

```bash
npm run build
```

生产部署请使用仓库根目录的 `docker-compose.yml` 或 Dashboard `Dockerfile`。反向代理必须将 `/api` 转发到 Workbench API，并正确保留 Cookie、HTTPS 和 `X-Forwarded-*` 头。完整配置见 [Workbench 接入说明](../../../document/06-对接NexusAuth.Workbench.md) 与 [高级配置](../../../document/08-高级配置.md)。
