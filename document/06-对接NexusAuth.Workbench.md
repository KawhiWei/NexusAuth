# 对接 NexusAuth.Workbench

本文档介绍如何启动和使用 NexusAuth Workbench 管理端。

## 概述

NexusAuth.Workbench 是基于 NexusAuth 构建的管理端站点，用于管理 OAuth 客户端和 API 资源。

包含：
- **NexusAuth.Workbench.Api**：后端 API 服务 (端口: 5051)
- **NexusAuth.Workbench.Dashboard**：前端管理界面 (端口: 5273)

## 快速开始

### 启动服务

```bash
# 使用 NexusAuth.Workbench.sln 启动后端
cd NexusAuth.Workbench
dotnet run --project NexusAuth.Workbench.Api
```

服务启动在 http://localhost:5051

### API 端点

| 端点 | 方法 | 说明 |
|------|------|------|
| /api/auth/config | GET | 获取 OAuth 配置 |
| /api/auth/login | GET | 获取授权 URL |
| /signin-oidc | GET | OAuth 回调 |
| /api/auth/me | GET | 获取当前用户 |
| /api/auth/logout | POST | 退出登录 |
| /api/clients | GET/POST | Client 管理 |
| /api/clients/{id} | GET/PUT/DELETE | Client 详情 |
| /api/api-resources | GET/POST | API Resource 管理 |
| /api/api-resources/{id} | GET/PUT/DELETE | API Resource 详情 |

## 对接到现有项目

### 步骤 1：安装 NexusAuth.Extension

在项目中引用 NexusAuth.Extension：

```xml
<ProjectReference Include="..\NexusAuth.Extension\NexusAuth.Extension.csproj" />
```

或通过 NuGet 包引用（发布后）。

### 步骤 2：配置 appsettings.json

```json
{
  "Auth": {
    "Authority": "http://localhost:5100",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "RedirectUri": "http://localhost:5051/signin-oidc",
    "PostLogoutRedirectUri": "http://localhost:5273/",
    "Scope": "openid profile your-api-scope",
    "SignOutProvider": true
  }
}
```

| 配置项 | 说明 | 示例 |
|--------|------|------|
| Authority | NexusAuth Provider 地址 | http://localhost:5100 |
| ClientId | OAuth 客户端 ID | my-app-client |
| ClientSecret | OAuth 客户端密钥 | secret-key |
| RedirectUri | 授权回调地址 | http://localhost:5051/signin-oidc |
| PostLogoutRedirectUri | 登出回调地址 | http://localhost:5273/ |
| Scope | 授权的 scope | openid profile |
| SignOutProvider | 是否退出 Provider | true/false |

### 步骤 3：注册服务

在 AppModule 中配置：

```csharp
public override void ConfigureServices(ConfigureServicesContext context)
{
    var services = context.Services;
    var configuration = services.GetConfiguration();

    var authority = configuration["Auth:Authority"];
    var clientId = configuration["Auth:ClientId"];
    var clientSecret = configuration["Auth:ClientSecret"];
    var redirectUri = configuration["Auth:RedirectUri"];
    var postLogoutRedirectUri = configuration["Auth:PostLogoutRedirectUri"];
    var scope = configuration["Auth:Scope"];

    services.AddNexusAuth(options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.RedirectUri = redirectUri;
        options.PostLogoutRedirectUri = postLogoutRedirectUri;
        options.Scope = scope;
    });

    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = ".YourApp";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/api/auth/login";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
        });

    base.ConfigureServices(context);
}
```

### 步骤 4：实现 AuthController

参考 `NexusAuth.Workbench/NexusAuth.Workbench.Api/Controllers/AuthController.cs`：

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IFlowStateStore _flowStore;
    private readonly IOidcWorkbenchService _oidcService;

    public AuthController(IFlowStateStore flowStore, IOidcWorkbenchService oidcService)
    {
        _flowStore = flowStore;
        _oidcService = oidcService;
    }

    [HttpGet("/api/auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        var discovery = await _oidcService.FetchDiscoveryAsync(ct);
        var state = Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        var (codeChallenge, codeVerifier) = _oidcService.GeneratePkce();

        await _flowStore.AddAsync(state, new FlowState(codeVerifier, nonce), ct);

        var authorizeUrl = discovery.AuthorizationEndpoint +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(_oidcService.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_oidcService.RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(_oidcService.Scope)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&nonce={Uri.EscapeDataString(nonce)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        return Ok(new { authorizeUrl });
    }

    [HttpGet("/signin-oidc")]
    [AllowAnonymous]
    public async Task<IActionResult> SignInOidc(CancellationToken ct)
    {
        var code = Request.Query["code"].ToString();
        var state = Request.Query["state"].ToString();

        var flow = await _flowStore.GetAsync(state, ct);
        if (flow == null)
            return BadRequest(new { error = "invalid_state" });

        await _flowStore.RemoveAsync(state, ct);

        var (accessToken, idToken, expiresIn) = 
            await _oidcService.ExchangeCodeAsync(code, flow.CodeVerifier, ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, idToken),
            new(ClaimTypes.Name, idToken),
            new("access_token", accessToken),
            new("id_token", idToken),
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, 
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
            });

        return Redirect("/auth/callback");
    }
}
```

## 关键组件说明

### IFlowStateStore

用于存储 PKCE 流程中的 state 和 code_verifier：

- **InMemoryFlowStateStore**：内存存储（默认，仅开发环境）
- 自定义实现：可使用 Redis、数据库等

### IOidcWorkbenchService

核心 OAuth 服务：

- FetchDiscoveryAsync：获取 OIDC 发现文档
- GeneratePkce：生成 PKCE 码对
- ExchangeCodeAsync：交换授权码

## 下一步

- [启动 Dashboard](./07-对接NexusAuth.Workbench.Dashboard.md)