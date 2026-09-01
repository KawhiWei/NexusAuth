using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NexusAuth.Host.Authentication;

public sealed record SliderCaptchaChallenge(int TargetOffsetPixels, string Nonce)
{
    public int TargetPositionPixels => TargetOffsetPixels;
}

public sealed record SliderCaptchaChallengeTicket(string Token, int TargetOffsetPixels);

public sealed class SliderCaptchaChallengeProtector
{
    private const string Purpose = "NexusAuth.SliderCaptcha.Challenge.v1";

    private readonly ITimeLimitedDataProtector protector;
    private readonly SliderCaptchaOptions options;
    private readonly IMemoryCache cache;
    private readonly object consumptionLock = new();

    public SliderCaptchaChallengeProtector(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<SliderCaptchaOptions> options,
        IMemoryCache cache)
    {
        protector = dataProtectionProvider
            .CreateProtector(Purpose)
            .ToTimeLimitedDataProtector();
        this.options = options.Value;
        this.cache = cache;
    }

    public SliderCaptchaChallengeProtector(IDataProtectionProvider dataProtectionProvider)
        : this(dataProtectionProvider, Options.Create(new SliderCaptchaOptions()), new MemoryCache(new MemoryCacheOptions()))
    {
    }

    public SliderCaptchaChallengeTicket CreateChallenge()
    {
        var edgePaddingPixels = Math.Min(32, options.TrackWidthPixels / 3);
        var targetOffsetPixels = RandomNumberGenerator.GetInt32(
            edgePaddingPixels,
            checked(options.TrackWidthPixels - edgePaddingPixels + 1));
        var challenge = new SliderCaptchaChallenge(targetOffsetPixels, Guid.NewGuid().ToString("N"));
        return new SliderCaptchaChallengeTicket(
            Protect(challenge, TimeSpan.FromSeconds(options.ChallengeLifetimeSeconds)),
            targetOffsetPixels);
    }

    public string Protect(SliderCaptchaChallenge challenge, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "The challenge lifetime must be positive.");

        return protector.Protect(JsonSerializer.Serialize(challenge), lifetime);
    }

    public bool TryUnprotect(string? protectedChallenge, out SliderCaptchaChallenge? challenge)
    {
        challenge = null;
        if (string.IsNullOrWhiteSpace(protectedChallenge))
            return false;

        try
        {
            challenge = JsonSerializer.Deserialize<SliderCaptchaChallenge>(protector.Unprotect(protectedChallenge));
            return challenge is not null
                && !string.IsNullOrWhiteSpace(challenge.Nonce);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool TryValidate(string? protectedChallenge, int? offsetPixels)
    {
        return TryValidate(protectedChallenge, offsetPixels, out _);
    }

    public bool TryValidate(
        string? protectedChallenge,
        int? offsetPixels,
        out SliderCaptchaChallenge? challenge)
    {
        if (!TryUnprotect(protectedChallenge, out challenge)
            || challenge is null
            || offsetPixels is null)
        {
            return false;
        }

        if (challenge.TargetOffsetPixels < 0
            || challenge.TargetOffsetPixels > options.TrackWidthPixels
            || offsetPixels.Value < 0
            || offsetPixels.Value > options.TrackWidthPixels)
        {
            return false;
        }

        if (Math.Abs(challenge.TargetOffsetPixels - offsetPixels.Value) > options.TolerancePixels)
            return false;

        lock (consumptionLock)
        {
            var cacheKey = $"nexusauth:slider-captcha:consumed:{challenge.Nonce}";
            if (cache.TryGetValue(cacheKey, out _))
                return false;

            cache.Set(cacheKey, true, TimeSpan.FromSeconds(options.ChallengeLifetimeSeconds));
            return true;
        }
    }
}
