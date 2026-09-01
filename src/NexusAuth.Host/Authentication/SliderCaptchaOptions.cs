using Microsoft.Extensions.Options;

namespace NexusAuth.Host.Authentication;

public sealed class SliderCaptchaOptions
{
    public const string SectionName = "SliderCaptcha";

    public bool Enabled { get; set; }

    public int ChallengeLifetimeSeconds { get; set; } = 120;

    public int TolerancePixels { get; set; } = 5;

    public int TrackWidthPixels { get; set; } = 300;
}

public sealed class SliderCaptchaOptionsValidator : IValidateOptions<SliderCaptchaOptions>
{
    public ValidateOptionsResult Validate(string? name, SliderCaptchaOptions options)
    {
        var errors = new List<string>();

        if (options.ChallengeLifetimeSeconds is < 1 or > 3600)
            errors.Add("SliderCaptcha:ChallengeLifetimeSeconds must be between 1 and 3600.");

        if (options.TrackWidthPixels is < 1 or > 2000)
            errors.Add("SliderCaptcha:TrackWidthPixels must be between 1 and 2000.");

        if (options.TolerancePixels < 0)
            errors.Add("SliderCaptcha:TolerancePixels must be zero or greater.");
        else if (options.TrackWidthPixels > 0 && options.TolerancePixels >= options.TrackWidthPixels)
            errors.Add("SliderCaptcha:TolerancePixels must be less than TrackWidthPixels.");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
