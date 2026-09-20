using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexusAuth.Workbench.Api.Controllers;

[ApiController]
public sealed class CurrentIdentityController : ControllerBase
{
    [HttpGet("/api/auth/me")]
    [AllowAnonymous]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { isAuthenticated = false });

        return Ok(new
        {
            isAuthenticated = true,
            user = new
            {
                id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
                name = User.Identity.Name
            }
        });
    }
}
