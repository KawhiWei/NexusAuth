using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Clients;

namespace NexusAuth.Workbench.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    [HttpGet]
    public async Task<List<OAuthClient>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        CancellationToken ct = default)
    {
        var clients = await _clientService.GetAllAsync(ct);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            clients = clients
                .Where(c => c.ClientId.ToLower().Contains(kw) || c.ClientName.ToLower().Contains(kw))
                .ToList();
        }

        if (isActive.HasValue)
        {
            clients = clients.Where(c => c.IsActive == isActive.Value).ToList();
        }

        return clients;
    }

    [HttpGet("{id:guid}")]
    public async Task<OAuthClient?> GetById(Guid id, CancellationToken ct = default)
    {
        return await _clientService.GetByIdAsync(id, ct);
    }

    [HttpPost]
    public async Task<OAuthClient> Create([FromBody] CreateClientRequest request, CancellationToken ct = default)
    {
        return await _clientService.CreateAsync(request, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<OAuthClient> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct = default)
    {
        return await _clientService.UpdateAsync(id, request, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task Delete(Guid id, CancellationToken ct = default)
    {
        await _clientService.DeleteAsync(id, ct);
    }
}