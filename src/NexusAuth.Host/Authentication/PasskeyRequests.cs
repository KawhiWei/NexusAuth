using Fido2NetLib;

namespace NexusAuth.Host.Authentication;

/// <summary>请求创建 Passkey 注册选项。</summary>
public sealed record PasskeyRegistrationOptionsRequest(string EnrollmentToken);

/// <summary>提交认证器生成的 attestation，并完成 Passkey 注册。</summary>
public sealed record PasskeyRegistrationVerificationRequest(string FlowToken, AuthenticatorAttestationRawResponse Credential, string? DisplayName);

/// <summary>请求无用户名 Passkey 登录选项，并保留原登录跳转状态。</summary>
public sealed record PasskeyAuthenticationOptionsRequest(string? ReturnUrl, bool RememberMe);

/// <summary>提交认证器生成的 assertion，并完成 Passkey 登录验证。</summary>
public sealed record PasskeyAuthenticationVerificationRequest(string FlowToken, AuthenticatorAssertionRawResponse Credential);
