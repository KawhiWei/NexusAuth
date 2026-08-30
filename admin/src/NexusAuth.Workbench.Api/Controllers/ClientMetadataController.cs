using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Luck.AspNetCore.ApiResults;
using NexusAuth.Application.Clients;

namespace NexusAuth.Workbench.Api.Controllers;

[Authorize]
[ApiController]
[ApiResultWrap]
[Route("api/client-metadata")]
public class ClientMetadataController(IClientMetadataService clientMetadataService) : ControllerBase
{
    [HttpGet]
    public async Task<ClientMetadataDto> Get(CancellationToken ct = default)
    {
        return await clientMetadataService.GetAsync(ct);
    }
}
