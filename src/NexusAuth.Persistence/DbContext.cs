using Luck.EntityFrameworkCore.DbContexts;
using Microsoft.EntityFrameworkCore;
using NexusAuth.Domain.AggregateRoots.ApiResources;
using NexusAuth.Domain.AggregateRoots.OAuthClients;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence;

public class DbContext(DbContextOptions options) : LuckDbContextBase(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    public DbSet<ApiResource> ApiResources => Set<ApiResource>();

    public DbSet<ClientApiResource> ClientApiResources => Set<ClientApiResource>();

    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<DeviceAuthorization> DeviceAuthorizations => Set<DeviceAuthorization>();

    public DbSet<TokenBlacklistEntry> TokenBlacklistEntries => Set<TokenBlacklistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("nexusauth");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
