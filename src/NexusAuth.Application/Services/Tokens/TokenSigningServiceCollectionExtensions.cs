using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<ITokenSigningCredentialsProvider, RsaTokenSigningCredentialsProvider>();
        services.AddHostedService<TokenSigningCredentialsValidationHostedService>();

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
