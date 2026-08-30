using Luck.AspNetCore.ApiResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application;
using NexusAuth.Application.Services.LoginAudits;

namespace NexusAuth.Workbench.Api.Controllers;

[Authorize]
[ApiController]
[ApiResultWrap]
[Route("api/login-audits")]
public sealed class LoginAuditsController(ILoginAuditService loginAuditService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<LoginAuditLogDto>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] bool? isSuccessful,
        [FromQuery] string? clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        return loginAuditService.GetPagedAsync(keyword, isSuccessful, clientId, page, pageSize, ct);
    }
}
