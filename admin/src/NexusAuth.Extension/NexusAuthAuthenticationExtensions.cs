using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace NexusAuth.Extension;

/// <summary>
/// Registers JWT bearer validation against a NexusAuth OIDC provider.
/// </summary>
public static class NexusAuthAuthenticationExtensions
{
    public static AuthenticationBuilder AddNexusAuthJwtBearer(
        this AuthenticationBuilder builder,
        Action<NexusAuthJwtBearerOptions> configure)
        => AddNexusAuthJwtBearer(builder, JwtBearerDefaults.AuthenticationScheme, configure);

    /// <summary>
    /// Registers a named JWT bearer scheme that validates NexusAuth access tokens.
    /// </summary>
    public static AuthenticationBuilder AddNexusAuthJwtBearer(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<NexusAuthJwtBearerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new NexusAuthJwtBearerOptions();
        configure(settings);
        settings.Validate();

        return builder.AddJwtBearer(authenticationScheme, options =>
        {
            options.Authority = settings.Authority.TrimEnd('/');
            options.Audience = settings.Audience;
            options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Authority.TrimEnd('/'),
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = settings.ClockSkew,
                NameClaimType = settings.NameClaimType,
                RoleClaimType = settings.RoleClaimType,
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (settings.RequireAccessTokenUse
                        && !string.Equals(
                            context.Principal?.FindFirst("token_use")?.Value,
                            "access_token",
                            StringComparison.Ordinal))
                    {
                        context.Fail("Only access tokens are accepted.");
                    }

                    return Task.CompletedTask;
                },
            };
        });
    }
}

public sealed class NexusAuthJwtBearerOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool RequireAccessTokenUse { get; set; } = true;
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
    public string NameClaimType { get; set; } = "name";
    public string RoleClaimType { get; set; } = "role";

    internal void Validate()
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("NexusAuth JWT Authority must be an absolute HTTP(S) URL.");
        if (RequireHttpsMetadata && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("NexusAuth JWT Authority must use HTTPS when RequireHttpsMetadata is enabled.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("NexusAuth JWT Audience is required.");
        if (ClockSkew < TimeSpan.Zero)
            throw new InvalidOperationException("NexusAuth JWT ClockSkew cannot be negative.");
    }
}
