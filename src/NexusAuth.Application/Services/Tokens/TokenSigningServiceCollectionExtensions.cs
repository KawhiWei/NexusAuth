using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NexusAuth.Application.Services.Tokens;

public static class TokenSigningServiceCollectionExtensions
{
    public static IServiceCollection AddNexusAuthTokenSigning(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, TokenService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITokenSigningKeySource, CertificateTokenSigningKeySource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITokenSigningKeySource, RsaKeyFileTokenSigningKeySource>());
        services.AddSingleton<ITokenSigningCredentialsProvider, TokenSigningCredentialsProvider>();
        services.AddSingleton<IIdTokenHintValidator, IdTokenHintValidator>();
        services.AddHostedService<TokenSigningCredentialsValidationHostedService>();

        return services;
    }

    public static IServiceCollection AddNexusAuthTokenSigningKeySource<TSource>(this IServiceCollection services)
        where TSource : class, ITokenSigningKeySource
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITokenSigningKeySource, TSource>());
        return services;
    }
}

internal sealed class TokenSigningCredentialsValidationHostedService : IHostedService
{
    public TokenSigningCredentialsValidationHostedService(
        ITokenSigningCredentialsProvider signingCredentialsProvider)
    {
        ArgumentNullException.ThrowIfNull(signingCredentialsProvider);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
