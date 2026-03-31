using System.Security.Cryptography;
using Luck.DDD.Domain.Domain.Entities;

namespace NexusAuth.Domain.Entities;

public class AuthorizationCode : EntityWithIdentity<Guid>
{
    public string Code { get; private set; } = default!;

    public string ClientId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public string RedirectUri { get; private set; } = default!;

    public string Scope { get; private set; } = default!;

    public string? CodeChallenge { get; private set; }

    public string? CodeChallengeMethod { get; private set; }

    public bool IsUsed { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// EF Core constructor
    /// </summary>
    private AuthorizationCode(Guid id) : base(id)
    {
    }

    public static AuthorizationCode Create(
        string clientId,
        Guid userId,
        string redirectUri,
        string scope,
        string? codeChallenge = null,
        string? codeChallengeMethod = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var now = DateTimeOffset.UtcNow;

        return new AuthorizationCode(Guid.NewGuid())
        {
            Code = GenerateUrlSafeRandomString(32),
            ClientId = clientId,
            UserId = userId,
            RedirectUri = redirectUri,
            Scope = scope,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            IsUsed = false,
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now,
        };
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
    }

    private static string GenerateUrlSafeRandomString(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
