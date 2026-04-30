using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application;
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

    [HttpGet("all")]
    public async Task<List<ClientDto>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        CancellationToken ct = default)
    {
        return await _clientService.GetAllAsync(keyword, isActive, ct);
    }

    [HttpGet]
    public async Task<PagedResult<ClientDto>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        return await _clientService.GetPagedAsync(keyword, isActive, page, pageSize, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var client = await _clientService.GetByIdAsync(id, ct);
        if (client is null)
            return NotFound();

        return client;
    }

    [HttpPost]
    public async Task<ClientDto> Create([FromBody] CreateClientRequest request, CancellationToken ct = default)
    {
        return await _clientService.CreateAsync(request, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<ClientDto> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct = default)
    {
        return await _clientService.UpdateAsync(id, request, ct);
    }

    [HttpPost("{id:guid}/credentials")]
    public async Task<ClientMutationResultDto> GenerateCredential(
        Guid id,
        [FromBody] GenerateClientCredentialRequest request,
        CancellationToken ct = default)
    {
        return await _clientService.GenerateCredentialAsync(id, request, ct);
    }

    [HttpPost("{id:guid}/credentials/reset")]
    public async Task<ClientMutationResultDto> ResetCredential(
        Guid id,
        [FromBody] GenerateClientCredentialRequest request,
        CancellationToken ct = default)
    {
        return await _clientService.ResetCredentialAsync(id, request, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task Delete(Guid id, CancellationToken ct = default)
    {
        await _clientService.DeleteAsync(id, ct);
    }
}
