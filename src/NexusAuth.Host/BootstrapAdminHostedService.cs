using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Repositories;

namespace NexusAuth.Host;

public sealed class BootstrapAdminHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BootstrapAdminOptions> options,
    ILogger<BootstrapAdminHostedService> logger) : IHostedService
{
    private readonly BootstrapAdminOptions options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        if (!hasUsername && !hasPassword)
        {
            logger.LogInformation("Bootstrap administrator creation skipped because no credentials were configured.");
            return;
        }

        if (!hasUsername || !hasPassword)
            throw new InvalidOperationException("BootstrapAdmin:Username and BootstrapAdmin:Password must be configured together.");

        using var scope = scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var username = options.Username!.Trim();
        var existingUser = await userRepository.FindByUsernameAsync(username, cancellationToken);
        if (existingUser is not null)
        {
            if (!existingUser.IsSystemAccount)
            {
                existingUser.MarkAsSystemAccount();
                await userRepository.UpdateAsync(existingUser, cancellationToken);
            }

            logger.LogInformation(
                "Bootstrap administrator already exists. Existing password was preserved. UserId={UserId}",
                existingUser.Id);
            return;
        }

        var nickname = string.IsNullOrWhiteSpace(options.Nickname) ? username : options.Nickname.Trim();
        var user = User.Create(
            username,
            options.Password!,
            nickname,
            string.IsNullOrWhiteSpace(options.Email) ? null : options.Email.Trim());
        user.MarkAsSystemAccount();
        await userRepository.AddAsync(user, cancellationToken);

        logger.LogInformation("Bootstrap administrator created. UserId={UserId}", user.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
