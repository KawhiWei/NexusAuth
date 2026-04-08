## 1. 工程基础设施（Directory.Packages.props）

- [x] 1.1 在 `Directory.Packages.props` 中新增 `Microsoft.AspNetCore.Authentication.JwtBearer` 版本声明（9.0.x）
- [x] 1.2 在 `Directory.Packages.props` 中新增 `System.IdentityModel.Tokens.Jwt` 版本声明（8.x / 与 AspNetCore 9.0 兼容版本）
- [x] 1.3 在 `Directory.Packages.props` 中新增 `BCrypt.Net-Next` 版本声明（4.x）
- [x] 1.4 确认 `Directory.Build.props` 中 `ManagePackageVersionsCentrally` 为 `true`，所有新增包均不在 `.csproj` 中写版本号

## 2. Domain 层 — 用户身份（user-identity）

- [x] 2.1 在 `NexusAuth.Domain` 中创建 `User` 聚合根类（Id、Username、PasswordHash、Email、PhoneNumber、IsActive、CreatedAt、UpdatedAt）
- [x] 2.2 在 `User` 上实现静态工厂方法 `Create(username, rawPassword, email?, phoneNumber?)`，内部调用 BCrypt 哈希密码
- [x] 2.3 在 `User` 上实现 `VerifyPassword(rawPassword)` 方法，使用 BCrypt 验证
- [x] 2.4 在 `NexusAuth.Domain` 中定义 `IUserRepository` 接口（FindByUsernameAsync、FindByEmailAsync、FindByPhoneNumberAsync、FindByIdAsync、AddAsync）
- [x] 2.5 在 `NexusAuth.Application` 中添加对 `BCrypt.Net-Next` 的包引用（`<PackageReference Include="BCrypt.Net-Next" />`）
- [x] 2.6 在 `NexusAuth.Application` 中创建 `UserService`，实现 `RegisterAsync` 和 `ValidateCredentialsAsync`（支持 username/email/phone 三种方式登录）

## 3. Domain 层 — OAuth2 Client（client-management）

- [x] 3.1 在 `NexusAuth.Domain` 中创建 `OAuthClient` 聚合根类（Id、ClientId、ClientSecretHash、ClientName、Description、RedirectUris、AllowedScopes、AllowedGrantTypes、RequirePkce、IsActive、CreatedAt）
- [x] 3.2 在 `OAuthClient` 上实现静态工厂方法 `Create(...)`，内部 BCrypt 哈希 ClientSecret
- [x] 3.3 在 `OAuthClient` 上实现 `VerifyClientSecret(rawSecret)` 方法
- [x] 3.4 在 `OAuthClient` 上实现 `IsValidRedirectUri(uri)` 方法
- [x] 3.5 在 `OAuthClient` 上实现 `IsGrantTypeAllowed(grantType)` 方法
- [x] 3.6 在 `NexusAuth.Domain` 中定义 `IOAuthClientRepository` 接口（FindByClientIdAsync、AddAsync）
- [x] 3.7 在 `NexusAuth.Application` 中创建 `ClientService`，实现 `RegisterClientAsync` 和 `ValidateClientAsync`

## 4. Domain 层 — Authorization Code（oauth2-core）

- [x] 4.1 在 `NexusAuth.Domain` 中创建 `AuthorizationCode` 实体类（Id、Code、ClientId、UserId、RedirectUri、Scope、CodeChallenge、CodeChallengeMethod、IsUsed、ExpiresAt、CreatedAt）
- [x] 4.2 在 `AuthorizationCode` 上实现静态工厂方法，Code 使用 `RandomNumberGenerator` 生成 URL-safe 随机字符串（≥32 字符），ExpiresAt 默认 10 分钟后
- [x] 4.3 在 `NexusAuth.Domain` 中定义 `IAuthorizationCodeRepository` 接口（FindByCodeAsync、AddAsync、MarkUsedAsync）
- [x] 4.4 在 `NexusAuth.Application` 中创建 `AuthorizationService`，实现 `GenerateCodeAsync`（生成并持久化授权码）
- [x] 4.5 在 `AuthorizationService` 中实现 `ValidateAndConsumeCodeAsync`（校验授权码有效性、PKCE S256 校验、标记已用）
- [x] 4.6 在 `AuthorizationService` 中实现 `ValidateClientCredentialsAsync`（Client Credentials 流程校验）

## 5. Domain 层 — Refresh Token（token-management）

- [x] 5.1 在 `NexusAuth.Domain` 中创建 `RefreshToken` 实体类（Id、Token、ClientId、UserId、Scope、IsRevoked、ExpiresAt、CreatedAt）
- [x] 5.2 在 `RefreshToken` 上实现静态工厂方法，Token 使用 `RandomNumberGenerator` 生成 URL-safe 随机字符串（≥64 字符），ExpiresAt 默认 30 天后
- [x] 5.3 在 `NexusAuth.Domain` 中定义 `IRefreshTokenRepository` 接口（FindByTokenAsync、AddAsync、RevokeAsync、RevokeAllForUserAsync）

## 6. Application 层 — Token 服务（token-management）

- [x] 6.1 在 `NexusAuth.Application` 中添加 `Microsoft.AspNetCore.Authentication.JwtBearer` 和 `System.IdentityModel.Tokens.Jwt` 包引用
- [x] 6.2 创建 `JwtOptions` 配置类（SigningKey、Issuer、Audience、AccessTokenLifetimeMinutes）
- [x] 6.3 在 `NexusAuth.Application` 中创建 `TokenService`，实现 `IssueAccessTokenAsync`（颁发 HS256 JWT，payload 含 sub/client_id/scope/iat/exp/jti）
- [x] 6.4 在 `TokenService` 中实现 `IssueRefreshTokenAsync`（生成并持久化 RefreshToken，返回 Token 字符串）
- [x] 6.5 在 `TokenService` 中实现 `RefreshAsync`（校验旧 Refresh Token → 吊销旧 Token → 颁发新 Access Token + 新 Refresh Token）
- [x] 6.6 在 `TokenService` 中实现 `RevokeRefreshTokenAsync`（吊销指定 Token）
- [x] 6.7 在 `TokenService` 中实现 `RevokeAllUserTokensAsync`（吊销用户所有 Refresh Token）

## 7. Domain 层 & Application 层 — API 资源管理（api-resource-management）

- [x] 7.1 在 `NexusAuth.Domain` 中创建 `ApiResource` 聚合根类（Id、Name、DisplayName、Description、IsActive、CreatedAt）
- [x] 7.2 在 `NexusAuth.Domain` 中创建 `ClientApiResource` 值对象/关联实体（ClientId、ApiResourceId，联合主键）
- [x] 7.3 在 `NexusAuth.Domain` 中定义 `IApiResourceRepository` 接口（FindByNameAsync、FindByIdAsync、GetAllActiveAsync、AddAsync）
- [x] 7.4 在 `NexusAuth.Domain` 中定义 `IClientApiResourceRepository` 接口（GetResourcesByClientIdAsync、AddAsync、RemoveAsync）
- [x] 7.5 在 `NexusAuth.Application` 中创建 `ApiResourceService`，实现 `RegisterAsync`、`AssignToClientAsync`、`RevokeFromClientAsync`、`GetClientResourcesAsync`、`GetAllActiveResourcesAsync`
- [x] 7.6 在 `AuthorizationService` 中新增 scope 校验逻辑：验证请求的每个 scope 在 `ApiResource` 表中存在且 active，同时在 `OAuthClient.AllowedScopes` 中

## 8. Persistence 层 — EF 配置（persistence-mappings）

- [x] 8.1 在 `NexusAuth.Persistence` 中创建 `UserConfiguration : IEntityTypeConfiguration<User>`，完成 `users` 表字段映射（含 username/email/phone_number 唯一索引）
- [x] 8.2 在 `NexusAuth.Persistence` 中创建 `OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>`，完成 `oauth_clients` 表映射（RedirectUris/AllowedScopes/AllowedGrantTypes 存储为 jsonb 列）
- [x] 8.3 在 `NexusAuth.Persistence` 中创建 `ApiResourceConfiguration : IEntityTypeConfiguration<ApiResource>`，完成 `api_resources` 表映射（含 name 唯一索引）
- [x] 8.4 在 `NexusAuth.Persistence` 中创建 `ClientApiResourceConfiguration : IEntityTypeConfiguration<ClientApiResource>`，完成 `client_api_resources` 表映射（联合主键 client_id + api_resource_id）
- [x] 8.5 在 `NexusAuth.Persistence` 中创建 `AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>`，完成 `authorization_codes` 表映射（含 code 唯一索引）
- [x] 8.6 在 `NexusAuth.Persistence` 中创建 `RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>`，完成 `refresh_tokens` 表映射（含 token 唯一索引）
- [x] 8.7 在 `NexusAuthDbContext` 中新增 `DbSet<User> Users`、`DbSet<OAuthClient> OAuthClients`、`DbSet<ApiResource> ApiResources`、`DbSet<ClientApiResource> ClientApiResources`、`DbSet<AuthorizationCode> AuthorizationCodes`、`DbSet<RefreshToken> RefreshTokens` 属性
- [x] 8.8 在 `NexusAuth.Persistence` 中实现 `IUserRepository`（`UserRepository`）
- [x] 8.9 在 `NexusAuth.Persistence` 中实现 `IOAuthClientRepository`（`OAuthClientRepository`）
- [x] 8.10 在 `NexusAuth.Persistence` 中实现 `IApiResourceRepository`（`ApiResourceRepository`）
- [x] 8.11 在 `NexusAuth.Persistence` 中实现 `IClientApiResourceRepository`（`ClientApiResourceRepository`）
- [x] 8.12 在 `NexusAuth.Persistence` 中实现 `IAuthorizationCodeRepository`（`AuthorizationCodeRepository`）
- [x] 8.13 在 `NexusAuth.Persistence` 中实现 `IRefreshTokenRepository`（`RefreshTokenRepository`）
- [x] 8.14 在 `EntityFrameworkCoreModule` 中注册六个仓储实现（`services.AddScoped<IXxxRepository, XxxRepository>()`）

## 9. 构建验证

- [x] 9.1 在解决方案根目录运行 `dotnet build` 确认所有项目无编译错误
- [x] 9.2 确认所有新增包均无版本号直接写在 `.csproj` 中（全部通过 `Directory.Packages.props` 管理）

## 10. User 用户画像字段扩展（Gender、Nickname、Ethnicity）

- [x] 10.1 在 `NexusAuth.Domain` 中新增 `Gender` 枚举类型（Unknown = 0, Male = 1, Female = 2）
- [x] 10.2 在 `User` 聚合根中新增属性：`Nickname`（string, required）、`Gender`（Gender 枚举）、`Ethnicity`（string?, nullable）
- [x] 10.3 更新 `User.Create(...)` 静态工厂方法，新增 `nickname` 必填参数、`gender` 可选参数（默认 Unknown）、`ethnicity` 可选参数
- [x] 10.4 更新 `UserService.RegisterAsync` 方法签名，新增 `nickname` 必填参数、`gender` 和 `ethnicity` 可选参数，传递给 `User.Create`
- [x] 10.5 更新 `UserConfiguration`（Persistence 层），新增 `nickname`（varchar(100), NOT NULL）、`gender`（smallint, NOT NULL, DEFAULT 0）、`ethnicity`（varchar(50), NULLABLE）列映射
- [x] 10.6 运行 `dotnet build` 确认编译通过

## 11. IScopedDependency 重构 — 自动依赖注入

- [x] 11.1 在 `NexusAuth.Domain.csproj` 中新增 `Luck.Framework` 包引用
- [x] 11.2 让所有 6 个仓储接口（`IUserRepository`、`IOAuthClientRepository`、`IAuthorizationCodeRepository`、`IRefreshTokenRepository`、`IApiResourceRepository`、`IClientApiResourceRepository`）继承 `IScopedDependency`
- [x] 11.3 在 `NexusAuth.Application.csproj` 中新增 `Luck.Framework` 包引用
- [x] 11.4 创建 `IUserService` 接口（继承 `IScopedDependency`）
- [x] 11.5 创建 `IClientService` 接口（继承 `IScopedDependency`）
- [x] 11.6 创建 `IAuthorizationService` 接口（继承 `IScopedDependency`）
- [x] 11.7 创建 `ITokenService` 接口（继承 `IScopedDependency`）
- [x] 11.8 创建 `IApiResourceService` 接口（继承 `IScopedDependency`）
- [x] 11.9 更新 5 个服务类实现各自接口（`UserService : IUserService`、`ClientService : IClientService`、`AuthorizationService : IAuthorizationService`、`TokenService : ITokenService`、`ApiResourceService : IApiResourceService`）
- [x] 11.10 从 `EntityFrameworkCoreModule` 中移除 6 个手动 `services.AddScoped<>()` 注册（由 `Luck.AutoDependencyInjection` 自动扫描注册）
- [x] 11.11 运行 `dotnet build` 确认编译通过

## 12. Host 层 — MVC Host 项目（NexusAuth.Host）

- [x] 12.1 创建 `NexusAuth.Host` 项目目录及 `.csproj`（Web SDK，引用 Application、Persistence、Shared、Luck.AutoDependencyInjection、Luck.AspNetCore）
- [x] 12.2 将 `NexusAuth.Host` 添加到 `NexusAuth.sln`
- [x] 12.3 创建 `AppWebModule.cs`（根启动模块，`[DependsOn(AutoDependencyAppModule, EntityFrameworkCoreModule)]`，配置 `JwtOptions`）
- [x] 12.4 创建 `Program.cs`（Luck 启动模式：`AddApplication<AppWebModule>()`、`MapControllers()`、`InitializeApplication()`）
- [x] 12.5 创建 `appsettings.json` 和 `appsettings.Development.json`（ConnectionStrings:Default、Jwt 配置段）
- [x] 12.6 更新 `EntityFrameworkCoreModule`，通过 `services.GetConfiguration()` 从 `IConfiguration` 读取连接字符串
- [x] 12.7 创建 `AuthorizeController`（GET + POST `/connect/authorize`，Authorization Code + PKCE 流程）
- [x] 12.8 创建 `TokenController`（POST `/connect/token`，支持 authorization_code、client_credentials、refresh_token 三种 grant_type）
- [x] 12.9 运行 `dotnet build` 确认全量编译通过（0 错误）
- [x] 12.10 更新 `tasks.md` 添加 Group 12 任务列表

## 13. 仓储重构 & Host 层隔离（repository-refactoring）

- [x] 13.1 重构 3 个聚合根仓储接口（`IUserRepository`、`IOAuthClientRepository`、`IApiResourceRepository`），继承 `IAggregateRootRepository<TEntity, Guid>` + `IScopedDependency`
- [x] 13.2 重构 3 个实体仓储接口（`IAuthorizationCodeRepository`、`IRefreshTokenRepository`、`IClientApiResourceRepository`），继承 `IEntityRepository<TEntity, Guid>` + `IScopedDependency`
- [x] 13.3 重构 3 个聚合根仓储实现（`UserRepository`、`OAuthClientRepository`、`ApiResourceRepository`），继承 `EfCoreAggregateRootRepository<TEntity, Guid>`，构造函数传入 `IUnitOfWork`
- [x] 13.4 重构 3 个实体仓储实现（`AuthorizationCodeRepository`、`RefreshTokenRepository`、`ClientApiResourceRepository`），继承 `EfCoreEntityRepository<TEntity, Guid>`，构造函数传入 `IUnitOfWork`
- [x] 13.5 在 `IClientService` 中新增 `ValidateClientForAuthorizationAsync` 方法，将客户端验证逻辑封装到 Application 层（含 `ClientValidationResult` 记录类型）
- [x] 13.6 在 `ClientService` 中实现 `ValidateClientForAuthorizationAsync`（检查客户端存在性、激活状态、回调地址、授权类型、PKCE 要求）
- [x] 13.7 重构 `AuthorizeController`，移除 `IOAuthClientRepository` 直接依赖，改为仅通过 `IClientService` 调用 Application 层
- [x] 13.8 验证 Host 层（`NexusAuth.Host`）无任何 `NexusAuth.Domain.Repositories` 引用
- [x] 13.9 运行 `dotnet build` 确认全量编译通过（0 错误）
- [x] 13.10 更新 `tasks.md` 添加 Group 13 任务列表

## 14. 登录界面 & Cookie 会话管理（login-page-sso）

- [x] 14.1 在 `Program.cs` 中添加 `AddRazorPages()` 和 `MapRazorPages()` 注册 Razor Pages 支持
- [x] 14.2 在 `AppWebModule.cs` 中配置 ASP.NET Core Cookie Authentication（Scheme: `NexusAuth.Identity`，滑动过期 30 分钟 + 绝对过期 24 小时）
- [x] 14.3 在 `AppWebModule.ApplicationInitialization` 中添加 `UseAuthentication()` 中间件（在 `UseAuthorization()` 之前）
- [x] 14.4 创建 `Pages/_ViewImports.cshtml`（引入 TagHelper）
- [x] 14.5 创建 `Pages/Account/Login.cshtml`（登录表单 HTML 页面，含内联 CSS 样式）
- [x] 14.6 创建 `Pages/Account/Login.cshtml.cs`（LoginModel PageModel：GET 检查已登录则直接跳转、POST 验证凭据并签发 Cookie）
- [x] 14.7 重构 `AuthorizeController`：移除 POST 端点，GET 检查 Cookie 已登录则直接签发授权码并 302 回调，未登录则 302 到 `/account/login?returnUrl=...`
- [x] 14.8 从 `AuthorizeController` 移除 `IUserService` 依赖（用户认证逻辑已移至 Login Page）
- [x] 14.9 运行 `dotnet build` 确认全量编译通过（0 错误）
- [x] 14.10 更新 `tasks.md` 添加 Group 14 任务列表

## 15. Demo 项目 — 前后端分离 OAuth2 SSO 演示（demo-app）

- [x] 15.1 创建 `demo/DemoApp.Api/` 项目目录结构及 `DemoApp.Api.csproj`（Web SDK，CPM 兼容，引用 JwtBearer + JWT 包）
- [x] 15.2 创建 `demo/DemoApp.Api/Properties/launchSettings.json`（端口 5010）
- [x] 15.3 创建 `src/NexusAuth.Host/Properties/launchSettings.json`（端口 5000）
- [x] 15.4 创建 `demo/DemoApp.Api/Program.cs`（Minimal API：JWT Bearer 验证、PKCE 流程、`/api/login`、`/callback`、`/api/token`、`/api/refresh`、`/api/me` 端点）
- [x] 15.5 创建 `demo/DemoApp.Api/appsettings.json`（NexusAuth authority URL、客户端凭据、JWT 验证配置）
- [x] 15.6 创建 `demo/DemoApp.Api/wwwroot/index.html`（vanilla HTML/JS SPA 前端，含登录、Token 显示、Claims 表格、PKCE 流程图）
- [x] 15.7 创建 `demo/schema.sql`（PostgreSQL DDL 脚本，6 张表 + 索引，使用 `IF NOT EXISTS` 幂等）
- [x] 15.8 创建 `demo/seed.sql`（演示数据种子脚本：demo OAuth 客户端、demo 用户、API 资源，使用 `ON CONFLICT` 幂等）
- [x] 15.9 在 `AppWebModule.cs` 中配置 CORS（从 `appsettings.json` 读取 `Cors:AllowedOrigins`，允许 `http://localhost:5010`）
- [x] 15.10 在 `AppWebModule.ApplicationInitialization` 中添加 `UseCors()` 中间件（在 `UseAuthentication()` 之前）
- [x] 15.11 在 `appsettings.json` 中添加 `Cors:AllowedOrigins` 配置项
- [x] 15.12 运行 `dotnet build` 确认 NexusAuth.sln 和 DemoApp.Api 全量编译通过（0 错误）
- [x] 15.13 更新 `tasks.md` 添加 Group 15 任务列表
