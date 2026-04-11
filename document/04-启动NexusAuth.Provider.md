# 启动 NexusAuth Provider

本文档介绍如何启动 NexusAuth OAuth Provider 服务。

## 启动服务

### 开发模式启动

```bash
cd src/NexusAuth.Host
dotnet run
```

服务启动成功后，访问：http://localhost:5100

### 配置说明

编辑 `src/NexusAuth.Host/appsettings.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nexusauth;Username=nexusauth;Password=your-password"
  },
  "AllowedHosts": "*",
  "App": {
    "Issuer": "http://localhost:5100",
    "SigningKeyPath": "App_Data/signing-key.json"
  }
}
```

## 可用端点

| 端点 | 方法 | 说明 |
|------|------|------|
| /.well-known/openid-configuration | GET | OIDC 发现文档 |
| /connect/authorize | GET/POST | 授权端点 |
| /connect/token | POST | Token 端点 |
| /connect/userinfo | GET | 用户信息端点 |
| /connect/revocation | POST | 撤销端点 |
| /connect/endsession | GET | 登出端点 |
| /account/login | GET | 登录页面 |

## OIDC 发现文档

访问 `/.well-known/openid-configuration` 返回：

```json
{
  "issuer": "http://localhost:5100",
  "authorization_endpoint": "http://localhost:5100/connect/authorize",
  "token_endpoint": "http://localhost:5100/connect/token",
  "userinfo_endpoint": "http://localhost:5100/connect/userinfo",
  "jwks_uri": "http://localhost:5100/.well-known/jwks",
  "revocation_endpoint": "http://localhost:5100/connect/revocation",
  "end_session_endpoint": "http://localhost:5100/connect/endsession"
}
```

## 登录测试

1. 访问 http://localhost:5100/account/login
2. 使用默认账号登录（需先创建用户）
3. 登录成功后可看到用户信息页面

## 创建测试用户

需要通过数据库插入用户：

```sql
-- 插入测试用户
INSERT INTO users (id, username, email, password_hash, is_active, created_at)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'testuser',
    'test@example.com',
    '$2a$12$...', -- BCrypt 加密的密码
    true,
    NOW()
);
```

> 注意：实际项目中建议通过管理界面创建用户。

## 下一步

- [配置 OAuth 客户端](./05-配置OAuth客户端.md)