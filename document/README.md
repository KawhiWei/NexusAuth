# NexusAuth 文档

| 文档 | 内容 / 状态 |
|---|---|
| [快速启动](./01-快速启动.md) | 非 Gateway 镜像启动、全部环境变量、认证流程、管理操作、接入、开发、Demo、WebAuthn 和排错。统一入口。 |
| [OAuth/OIDC 协议设计](./11-OAuth-OIDC协议设计.md) | 保留协议设计与实现边界。 |
| [Workbench API 统一登录接入模板](./Workbench-API统一登录接入模板.md) | 服务端 OIDC 客户端、回调、会话及令牌刷新；用于参考，不要求业务系统采用 Workbench。 |
| [Workbench Dashboard 统一登录接入模板](./Workbench-Dashboard统一登录接入模板.md) | 浏览器通过同源 API 完成登录、会话检查和退出；不在前端保存 OAuth 凭据。 |
| [SMTP 发信试验](./SMTP发信试验.md) | 可选内部发信服务的配置和测试，尚未接入注册验证与密码找回。 |
| [APISIX 网关对接手册](./13-APISIX网关对接手册.md) | 暂未整理，仅供网关场景参考。 |

Provider 的 OAuth/OIDC 接入入口在[项目 README](../README.md)；部署和全部配置仍以快速启动手册为准。
