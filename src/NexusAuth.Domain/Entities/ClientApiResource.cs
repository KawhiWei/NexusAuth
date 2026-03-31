namespace NexusAuth.Domain.Entities;

/// <summary>
/// Join entity for Client ↔ ApiResource (composite PK: ClientId + ApiResourceId).
/// Does not use a surrogate Id.
/// </summary>
public class ClientApiResource
{
    public Guid ClientId { get; private set; }

    public Guid ApiResourceId { get; private set; }

    /// <summary>
    /// EF Core parameterless constructor
    /// </summary>
    private ClientApiResource()
    {
    }

    public static ClientApiResource Create(Guid clientId, Guid apiResourceId)
    {
        return new ClientApiResource
        {
            ClientId = clientId,
            ApiResourceId = apiResourceId,
        };
    }
}
