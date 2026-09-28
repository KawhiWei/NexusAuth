# SMTP 发信试验

Provider 内置 MailKit 发信服务，默认关闭。目前只提供内部 `IEmailSender`，尚未接入注册验证、密码找回或 HTTP 发信接口。

启用时设置以下环境变量；根目录 Compose 的 `sso` 服务也接受这些变量：

| 变量 | QQ 邮箱示例 | 163 邮箱示例 |
|---|---|---|
| `NEXUSAUTH_SMTP_ENABLED` | `true` | `true` |
| `NEXUSAUTH_SMTP_HOST` | `smtp.qq.com` | `smtp.163.com` |
| `NEXUSAUTH_SMTP_PORT` | `465` | `465` |
| `NEXUSAUTH_SMTP_SECURITY` | `SslOnConnect` | `SslOnConnect` |
| `NEXUSAUTH_SMTP_USERNAME` | 完整 QQ 邮箱地址 | 完整 163 邮箱地址 |
| `NEXUSAUTH_SMTP_PASSWORD` | SMTP 授权码 | SMTP 授权码 |
| `NEXUSAUTH_SMTP_FROM_ADDRESS` | 与登录账号一致的邮箱 | 与登录账号一致的邮箱 |
| `NEXUSAUTH_SMTP_FROM_NAME` | `NexusAuth` | `NexusAuth` |

邮箱服务支持时也可使用 `587` + `StartTls`。先在邮箱后台启用 SMTP 并生成授权码，不要使用邮箱登录密码；将授权码放在部署环境的 Secret 中，不要提交到仓库或写进镜像。运行环境需要能连接 SMTP 服务器。

先运行不发信的测试：

```bash
dotnet test tests/NexusAuth.Host.IntegrationTests/NexusAuth.Host.IntegrationTests.csproj --filter FullyQualifiedName~SmtpEmailSenderTests
```

真实试发使用被 Git 忽略的 `tests/NexusAuth.Host.IntegrationTests/smtp.qq.local.json`：填写完整 QQ 邮箱地址到 `Username`，把 SMTP 授权码填到 `AuthorizationCode`。测试固定使用 `smtp.qq.com:465`（SSL/TLS），收件人为 `18790997531@163.com`；运行前先确认该收件地址是预期目标：

```bash
dotnet test tests/NexusAuth.Host.IntegrationTests/NexusAuth.Host.IntegrationTests.csproj --filter FullyQualifiedName~Configured_live_qq_smtp_can_send_test_message --logger "console;verbosity=detailed"
```

账号或授权码未填写时会输出“未发送”，不对外发信。授权码不要提交到 Git。
