using Luck.DDD.Domain.Domain.AggregateRoots;

namespace NexusAuth.Domain.AggregateRoots.ApiResources;

public class ApiResource : AggregateRootWithIdentity<Guid>
{
    public string Name { get; private set; } = default!;

    public string DisplayName { get; private set; } = default!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// EF Core constructor
    /// </summary>
    private ApiResource(Guid id) : base(id)
    {
    }

    public static ApiResource Create(string name, string displayName, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new ApiResource(Guid.NewGuid())
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
