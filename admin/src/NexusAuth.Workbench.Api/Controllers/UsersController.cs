using Luck.AspNetCore.ApiResults;
using Luck.Framework.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusAuth.Application;
using NexusAuth.Application.Users;

namespace NexusAuth.Workbench.Api.Controllers;

[Authorize]
[ApiController]
[ApiResultWrap]
[Route("api/users")]
public sealed class UsersController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ManagedUserDto>> GetPaged([FromQuery] string? keyword, [FromQuery] bool? isActive, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => userManagementService.GetPagedAsync(keyword, isActive, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public async Task<ManagedUserDto> GetById(Guid id, CancellationToken ct = default)
        => await userManagementService.GetByIdAsync(id, ct) ?? throw new NotFoundException($"User with id '{id}' was not found.");

    [HttpPut("{id:guid}")]
    public Task<ManagedUserDto> Update(Guid id, [FromBody] UpdateManagedUserRequest request, CancellationToken ct = default)
        => userManagementService.UpdateProfileAsync(id, request, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<ManagedUserDto> UpdateStatus(Guid id, [FromBody] UpdateManagedUserStatusRequest request, CancellationToken ct = default)
        => userManagementService.UpdateStatusAsync(id, request.IsActive, ct);

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetManagedUserPasswordRequest request, CancellationToken ct = default)
    {
        await userManagementService.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }
}

public sealed record UpdateManagedUserStatusRequest(bool IsActive);
