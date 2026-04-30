namespace NexusAuth.Application.Clients;

public interface IClientMetadataService : IScopedDependency
{
    Task<ClientMetadataDto> GetAsync(CancellationToken ct = default);
}

public record ClientMetadataDto(
    List<ClientOptionDto> Scopes,
    List<ClientOptionDto> GrantTypes,
    List<ClientOptionDto> TokenEndpointAuthMethods);

public record ClientOptionDto(
    string Value,
    string Label,
    string Description);
