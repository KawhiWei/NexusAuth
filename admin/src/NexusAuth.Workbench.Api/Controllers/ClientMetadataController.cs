using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application.Clients;

namespace NexusAuth.Workbench.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/client-metadata")]
public class ClientMetadataController(IClientMetadataService clientMetadataService) : ControllerBase
{
    [HttpGet]
    public async Task<ClientMetadataDto> Get(CancellationToken ct = default)
    {
        return await clientMetadataService.GetAsync(ct);
    }
}
