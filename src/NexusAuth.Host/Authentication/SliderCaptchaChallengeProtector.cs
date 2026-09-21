using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NexusAuth.Host.Authentication;

public sealed record SliderCaptchaChallenge(int TargetOffsetPixels, string Nonce)
{
    public int TargetPositionPixels => TargetOffsetPixels;
}

public sealed record SliderCaptchaChallengeTicket(string Token, string ImageDataUrl);

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
            CreateChallengeImageDataUrl(targetOffsetPixels));
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

    private string CreateChallengeImageDataUrl(int targetOffsetPixels)
    {
        const int width = 600;
        const int height = 240;
        const int gapSize = 66;
        const int gapTop = 78;
        const int textureBlockSize = 20;
        var trackWidth = Math.Max(1, options.TrackWidthPixels);
        var gapLeft = (int)Math.Round(targetOffsetPixels / (double)trackWidth * (width - gapSize));
        var pixels = new byte[width * height * 3];
        var textureColumns = (width + textureBlockSize - 1) / textureBlockSize;
        var textureRows = (height + textureBlockSize - 1) / textureBlockSize;
        var texture = RandomNumberGenerator.GetBytes(textureColumns * textureRows);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 3;
                var textureOffset = (y / textureBlockSize) * textureColumns + (x / textureBlockSize);
                var variation = texture[textureOffset] % 24;
                var stripe = ((x + y) / 28) % 2 == 0 ? 14 : 0;
                pixels[offset] = (byte)(28 + variation);
                pixels[offset + 1] = (byte)(104 + variation + stripe);
                pixels[offset + 2] = (byte)(164 + variation + stripe);
            }
        }

        for (var y = gapTop; y < gapTop + gapSize; y++)
        {
            for (var x = gapLeft; x < gapLeft + gapSize; x++)
            {
                var offset = (y * width + x) * 3;
                var border = x < gapLeft + 4 || x >= gapLeft + gapSize - 4
                    || y < gapTop + 4 || y >= gapTop + gapSize - 4;
                if (border)
                {
                    pixels[offset] = 232;
                    pixels[offset + 1] = 244;
                    pixels[offset + 2] = 255;
                }
                else
                {
                    pixels[offset] = (byte)(pixels[offset] / 3);
                    pixels[offset + 1] = (byte)(pixels[offset + 1] / 3);
                    pixels[offset + 2] = (byte)(pixels[offset + 2] / 3);
                }
            }
        }

        return "data:image/png;base64," + Convert.ToBase64String(EncodePng(width, height, pixels));
    }

    private static byte[] EncodePng(int width, int height, byte[] rgbPixels)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> header = stackalloc byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var rowLength = width * 3;
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgbPixels, y * rowLength, rowLength);
            }
        }

        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);

        var crcInput = new byte[type.Length + data.Length];
        type.CopyTo(crcInput);
        data.CopyTo(crcInput.AsSpan(type.Length));
        Span<byte> crc = stackalloc byte[4];
        WriteBigEndian(crc, 0, ComputeCrc32(crcInput));
        output.Write(crc);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }

        return ~crc;
    }

    private static void WriteBigEndian(Span<byte> buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
