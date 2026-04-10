## 1. 工程基础设施

- [x] 1.1 在 `Directory.Packages.props` 中补充 JWT / BCrypt / EF Core 相关依赖版本声明
- [x] 1.2 保持 Central Package Management 统一依赖管理

## 2. Domain 层基础能力

- [x] 2.1 实现 `User` 聚合根与用户画像字段
- [x] 2.2 实现 `OAuthClient` 聚合根
- [x] 2.3 实现 `AuthorizationCode` 实体
- [x] 2.4 实现 `RefreshToken` 实体
- [x] 2.5 实现 `DeviceAuthorization` 实体
- [x] 2.6 实现 `TokenBlacklistEntry` 实体
- [x] 2.7 定义全部 Repository 接口

## 3. Application 层 OAuth2 / OIDC 能力

- [x] 3.1 实现 `UserService`
- [x] 3.2 实现 `ClientService`
- [x] 3.3 实现 `AuthorizationService`
- [x] 3.4 实现 `TokenService`
- [x] 3.5 实现 `DeviceAuthorizationService`
- [x] 3.6 实现 `ClientPrivateKeyJwtValidator`
- [x] 3.7 实现 `OidcClaimEmissionPolicy`
- [x] 3.8 实现 `OAuthClientAuthenticationParser`

## 4. Persistence 层持久化

- [x] 4.1 完成全部 EF Core 映射配置
- [x] 4.2 完成全部 Repository 实现
- [x] 4.3 接入 PostgreSQL
- [x] 4.4 形成 `demo/schema.sql`
- [x] 4.5 形成 `production-init.sql`

## 5. Host 层 Web 宿主

- [x] 5.1 创建 `NexusAuth.Host`
- [x] 5.2 提供 `/connect/authorize`
- [x] 5.3 提供 `/connect/token`
- [x] 5.4 提供 discovery / jwks / userinfo / revocation / introspection / endsession
- [x] 5.5 提供 device authorization 入口
- [x] 5.6 提供登录页 / consent 页 / device 验证页

## 6. 客户端认证方式扩展

- [x] 6.1 支持 `client_secret_basic`
- [x] 6.2 支持 `client_secret_post`
- [x] 6.3 支持 `private_key_jwt`
- [x] 6.4 将 client 凭据统一抽象为 `client_secrets`
- [x] 6.5 对 `private_key_jwt` 增加 `RS256` 白名单
- [x] 6.6 对 `private_key_jwt` 增加 `jti` 防重放

## 7. 授权模式扩展

- [x] 7.1 支持 `authorization_code + PKCE`
- [x] 7.2 向 OAuth 2.1 收敛，强制 `S256`
- [x] 7.3 支持 `client_credentials`
- [x] 7.4 支持 `refresh_token`
- [x] 7.5 支持 `device_code`

## 8. Demo 工程

- [x] 8.1 提供 `Demo.Bff` + `Demo.Web`（`private_key_jwt`）
- [x] 8.2 提供 `Demo.Bff.ClientSecret` + `Demo.Web.ClientSecret`（`client_secret_basic`）
- [x] 8.3 提供 `Demo.ClientCredentials`
- [x] 8.4 提供 `Demo.DeviceCode`
- [x] 8.5 提供 `Demo.RefreshToken`
- [x] 8.6 为每个 `private_key_jwt` demo/client 使用独立 key / JWKS

## 9. Refresh Token 测试与自动续期

- [x] 9.1 在 `Demo.DeviceCode` 中自动串联 refresh token 测试
- [x] 9.2 在 `Demo.Bff` 中实现基于 Cookie ValidatePrincipal 的自动 refresh
- [x] 9.3 缩短 access token / refresh token / cookie 生命周期以便本地测试

## 10. 文档与可视化

- [x] 10.1 更新 `README.md` 为完整使用文档
- [x] 10.2 新增 Mermaid 时序图
- [x] 10.3 新增 Mermaid 架构图
- [x] 10.4 新增 `diagram-index.md`
