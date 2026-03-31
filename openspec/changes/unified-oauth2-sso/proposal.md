## Why

NexusAuth 项目需要一个基于 OAuth2.0 协议的统一认证中心（SSO），为后续接入 QQ、微信、GitHub 等第三方登录提供统一入口与标准协议基础。当前项目已完成基础骨架（Domain、Application、Persistence、Shared 四个类库，基于 Luck 框架与 PostgreSQL），但尚无任何认证逻辑，需要从零构建核心 OAuth2.0 SSO 能力。

## What Changes

- 在 `Directory.Packages.props` 中补充 OAuth2.0/JWT 相关 NuGet 包版本声明（Central Package Management）
- 新增 `NexusAuth.Domain` 中的用户聚合根（User，含 Nickname、Gender 枚举、Ethnicity 等用户画像字段）、OAuth2 客户端聚合根（OAuthClient）、授权码实体（AuthorizationCode）、刷新令牌实体（RefreshToken）
- 新增 `NexusAuth.Domain` 中的仓储接口定义（IUserRepository、IOAuthClientRepository、IAuthorizationCodeRepository、IRefreshTokenRepository）
- 新增 `NexusAuth.Application` 中的应用服务：UserService（用户注册与凭证验证）、ClientService（OAuth2 Client 管理）、AuthorizationService（Authorization Code 流程 + PKCE 校验）、TokenService（JWT 颁发 + Refresh Token 管理）
- 新增 `NexusAuth.Persistence` 中的 EF 实体配置（`IEntityTypeConfiguration<T>`）、DbSet 注册与数据库迁移

## Capabilities

### New Capabilities

- `build-infrastructure`: 工程基础设施 —— 在 `Directory.Packages.props` 中补全 JWT/BCrypt/EF Design 相关包版本，统一项目依赖管理
- `user-identity`: 用户身份管理 —— 用户聚合根，支持账号/密码、手机号、邮箱三种登录凭证，含 BCrypt 密码哈希；包含用户画像字段（昵称 Nickname、性别 Gender 枚举、民族 Ethnicity）
- `client-management`: OAuth2 Client 管理 —— 注册与验证 OAuth2 应用（ClientId、ClientSecret、RedirectUri、Scope、GrantType）
- `oauth2-core`: OAuth2.0 授权核心 —— Authorization Code + PKCE 流程、Client Credentials 流程、Refresh Token 流程的领域逻辑与应用服务
- `token-management`: 令牌管理 —— JWT Access Token 颁发与验证，Refresh Token 持久化与吊销
- `persistence-mappings`: 持久化层映射 —— 使用 `IEntityTypeConfiguration<T>` 完成所有实体的数据库字段映射，覆盖所有新增实体
- `api-resource-management`: API 资源管理 —— 注册可保护的 API 资源（对应 OAuth2 scope），支持管理界面展示与为 OAuthClient 分配 API 资源访问权限；包含 `api_resources` 表与 `client_api_resources` 关联表

### Modified Capabilities

（`openspec/specs/` 目录当前为空，无已有规格需要修改）

## Impact

- **修改文件**: `Directory.Packages.props`（新增包版本声明）
- **修改项目**: `NexusAuth.Domain`（新增聚合根、实体、仓储接口）、`NexusAuth.Application`（新增应用服务、DTO）、`NexusAuth.Persistence`（新增 EF 配置、DbSet、迁移）
- **新增 NuGet 包**: `Microsoft.AspNetCore.Authentication.JwtBearer`、`System.IdentityModel.Tokens.Jwt`、`BCrypt.Net-Next`、`Microsoft.EntityFrameworkCore.Design`
- **数据库**: PostgreSQL，新增表 `users`、`oauth_clients`、`authorization_codes`、`refresh_tokens`
- **不影响**: 当前无 Host 项目，本次不创建 API 端点，仅实现领域逻辑与持久化层
