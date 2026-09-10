namespace NexusAuth.Application.Services.WebAuthn;

/// <summary>
/// WebAuthn 应用服务：负责生成流程令牌、维护一次性 challenge，并保存验证后的凭据状态。
/// FIDO2 协议验证仍由 Host 层完成，应用层只接收已经验证过的结果。
/// </summary>
public sealed class WebAuthnService(
    IWebAuthnChallengeRepository challengeRepository,
    IWebAuthnCredentialRepository credentialRepository) : IWebAuthnService
{
    public Task<IReadOnlyList<WebAuthnCredential>> GetEnabledCredentialsAsync(
        Guid userId,
        CancellationToken ct = default) =>
        credentialRepository.GetEnabledForUserAsync(userId, ct);

    public Task<string> CreateRegistrationChallengeAsync(
        Guid userId,
        string optionsJson,
        string? returnUrl,
        DateTimeOffset expiresAt,
        CancellationToken ct = default) =>
        CreateChallengeAsync(
            WebAuthnChallenge.RegistrationPurpose,
            userId,
            optionsJson,
            returnUrl,
            rememberMe: false,
            expiresAt,
            ct);

    public Task<string> CreateAuthenticationChallengeAsync(
        string optionsJson,
        string? returnUrl,
        bool rememberMe,
        DateTimeOffset expiresAt,
        CancellationToken ct = default) =>
        CreateChallengeAsync(
            WebAuthnChallenge.AuthenticationPurpose,
            userId: null,
            optionsJson,
            returnUrl,
            rememberMe,
            expiresAt,
            ct);

    public Task<WebAuthnChallenge?> ConsumeRegistrationChallengeAsync(
        string flowToken,
        DateTimeOffset now,
        CancellationToken ct = default) =>
        challengeRepository.TryConsumeAsync(flowToken, WebAuthnChallenge.RegistrationPurpose, now, ct);

    public Task<WebAuthnChallenge?> ConsumeAuthenticationChallengeAsync(
        string flowToken,
        DateTimeOffset now,
        CancellationToken ct = default) =>
        challengeRepository.TryConsumeAsync(flowToken, WebAuthnChallenge.AuthenticationPurpose, now, ct);

    public Task<WebAuthnCredential?> FindCredentialAsync(
        byte[] credentialId,
        CancellationToken ct = default) =>
        credentialRepository.FindByCredentialIdAsync(credentialId, ct);

    public Task RegisterCredentialAsync(
        WebAuthnCredentialRegistration registration,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        // 这里只保存服务端已验证的公钥材料，认证器私钥和生物信息不会进入 NexusAuth。
        var credential = WebAuthnCredential.Create(
            registration.UserId,
            registration.CredentialId,
            registration.PublicKeyCose,
            registration.SignatureCounter,
            registration.AaGuid,
            registration.Transports,
            registration.IsBackupEligible,
            registration.IsBackedUp,
            registration.DisplayName);

        return credentialRepository.AddAsync(credential, ct);
    }

    public async Task CompleteAuthenticationAsync(
        WebAuthnChallenge challenge,
        WebAuthnCredential credential,
        uint signatureCounter,
        bool isBackedUp,
        DateTimeOffset authenticatedAt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(credential);

        // 先持久化认证器计数器，再将由 Credential ID 解析出的可信用户回填到 challenge。
        credential.RecordAuthentication(signatureCounter, isBackedUp, authenticatedAt);
        await credentialRepository.UpdateAsync(credential, ct);

        challenge.AssignAuthenticatedUser(credential.UserId);
        await challengeRepository.UpdateAsync(challenge, ct);
    }

    private async Task<string> CreateChallengeAsync(
        string purpose,
        Guid? userId,
        string optionsJson,
        string? returnUrl,
        bool rememberMe,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        // 浏览器持有原始 bearer token，数据库只保存其 SHA-256 哈希。
        var flowToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebAuthnChallenge.Create(
            flowToken,
            purpose,
            userId,
            optionsJson,
            returnUrl,
            rememberMe,
            expiresAt);

        await challengeRepository.AddAsync(challenge, ct);
        return flowToken;
    }
}
