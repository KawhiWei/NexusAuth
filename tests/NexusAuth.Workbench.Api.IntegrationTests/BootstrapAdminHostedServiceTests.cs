using System.Linq.Expressions;
using Luck.DDD.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexusAuth.Domain.AggregateRoots.Users;
using NexusAuth.Domain.Repositories;
using NexusAuth.Host;
using Xunit;

namespace NexusAuth.Workbench.Api.IntegrationTests;

public sealed class BootstrapAdminHostedServiceTests
{
    [Fact]
    public async Task StartAsync_skips_when_no_bootstrap_credentials_are_configured()
    {
        var repository = new InMemoryUserRepository();
        var scopeFactory = new TestServiceScopeFactory(repository);
        var service = CreateService(scopeFactory, new BootstrapAdminOptions());

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(0, scopeFactory.CreateScopeCalls);
        Assert.Empty(repository.Users);
    }

    [Theory]
    [InlineData("admin", null)]
    [InlineData(null, "Secret123!")]
    public async Task StartAsync_fails_when_only_one_bootstrap_credential_is_configured(
        string? username,
        string? password)
    {
        var repository = new InMemoryUserRepository();
        var scopeFactory = new TestServiceScopeFactory(repository);
        var service = CreateService(scopeFactory, new BootstrapAdminOptions
        {
            Username = username,
            Password = password,
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("BootstrapAdmin:Username and BootstrapAdmin:Password", exception.Message);
        Assert.Equal(0, scopeFactory.CreateScopeCalls);
        Assert.Empty(repository.Users);
    }

    [Fact]
    public async Task StartAsync_creates_and_marks_a_missing_user_as_a_system_account()
    {
        var repository = new InMemoryUserRepository();
        var service = CreateService(
            new TestServiceScopeFactory(repository),
            new BootstrapAdminOptions
            {
                Username = "  admin  ",
                Password = "InitialSecret123!",
                Nickname = "  Platform Admin  ",
                Email = "  ADMIN@EXAMPLE.COM  ",
            });

        await service.StartAsync(CancellationToken.None);

        var created = Assert.Single(repository.Users);
        Assert.Equal("admin", created.Username);
        Assert.Equal("Platform Admin", created.Nickname);
        Assert.Equal("admin@example.com", created.Email);
        Assert.True(created.IsSystemAccount);
        Assert.True(created.VerifyPassword("InitialSecret123!"));
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task StartAsync_is_idempotent_for_an_existing_user_and_preserves_the_existing_password()
    {
        var existing = User.Create("admin", "ExistingSecret123!", "Existing Admin");
        var repository = new InMemoryUserRepository(existing);
        var scopeFactory = new TestServiceScopeFactory(repository);
        var service = CreateService(
            scopeFactory,
            new BootstrapAdminOptions
            {
                Username = "admin",
                Password = "ConfiguredSecret456!",
            });

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        Assert.Single(repository.Users);
        Assert.True(existing.IsSystemAccount);
        Assert.True(existing.VerifyPassword("ExistingSecret123!"));
        Assert.False(existing.VerifyPassword("ConfiguredSecret456!"));
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Equal(2, scopeFactory.CreateScopeCalls);
    }

    private static BootstrapAdminHostedService CreateService(
        IServiceScopeFactory scopeFactory,
        BootstrapAdminOptions options)
    {
        return new BootstrapAdminHostedService(
            scopeFactory,
            Options.Create(options),
            NullLogger<BootstrapAdminHostedService>.Instance);
    }

    private sealed class TestServiceScopeFactory(IUserRepository repository) : IServiceScopeFactory
    {
        public int CreateScopeCalls { get; private set; }

        public IServiceScope CreateScope()
        {
            CreateScopeCalls++;
            return new TestServiceScope(repository);
        }
    }

    private sealed class TestServiceScope(IUserRepository repository) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new TestServiceProvider(repository);

        public void Dispose()
        {
        }
    }

    private sealed class TestServiceProvider(IUserRepository repository) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(IUserRepository) ? repository : null;
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> users;

        public InMemoryUserRepository(params User[] initialUsers)
        {
            users = initialUsers.ToList();
        }

        public IReadOnlyList<User> Users => users;

        public int AddCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
        {
            return Task.FromResult<User?>(users.FirstOrDefault(user => user.Username == username));
        }

        public Task<User?> FindByExternalIdAsync(string externalId, CancellationToken ct = default)
        {
            return Task.FromResult<User?>(users.FirstOrDefault(user => user.ExternalId == externalId));
        }

        public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        {
            return Task.FromResult<User?>(users.FirstOrDefault(user => user.Email == email));
        }

        public Task<User?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default)
        {
            return Task.FromResult<User?>(users.FirstOrDefault(user => user.PhoneNumber == phoneNumber));
        }

        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult<User?>(users.FirstOrDefault(user => user.Id == id));
        }

        public Task<(IReadOnlyList<User> Items, int Total)> GetScimPagedAsync(
            string? userName,
            string? externalId,
            bool? isActive,
            string? email,
            int startIndex,
            int count,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<(IReadOnlyList<User> Items, int Total)> GetAdminPagedAsync(
            string? keyword,
            bool? isActive,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(User user, CancellationToken ct = default)
        {
            AddCalls++;
            users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken ct = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(User user, CancellationToken ct = default)
        {
            users.Remove(user);
            return Task.CompletedTask;
        }

        public Task<User?> RegisterFailedLoginAsync(
            Guid userId,
            int failureLimit,
            TimeSpan lockoutDuration,
            DateTimeOffset now,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task ResetLoginFailuresAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public User? Find(Guid primaryKey)
        {
            return users.FirstOrDefault(user => user.Id == primaryKey);
        }

        public ValueTask<User?> FindAsync(Guid primaryKey)
        {
            return ValueTask.FromResult(Find(primaryKey));
        }

        public Task<User?> FindAsync(Expression<Func<User, bool>> predicate)
        {
            return Task.FromResult(users.AsQueryable().FirstOrDefault(predicate));
        }

        public IQueryable<User> FindAll()
        {
            return users.AsQueryable();
        }

        public IQueryable<User> FindAll(Expression<Func<User, bool>> predicate)
        {
            return users.AsQueryable().Where(predicate);
        }

        public void Attach(User entity)
        {
        }

        public void Add(User entity)
        {
            AddCalls++;
            users.Add(entity);
        }

        public void Update(User entity)
        {
            UpdateCalls++;
        }

        public void Remove(User entity)
        {
            users.Remove(entity);
        }
    }
}
