using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Bff.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MachineProfileController : ControllerBase
{
    [HttpGet("/api/m2m/profile")]
    public IActionResult Profile()
    {
        return Ok(new
        {
            message = "This is a protected machine-to-machine API behind Demo.Bff.",
            clientId = User.FindFirst("client_id")?.Value,
            subject = User.FindFirst("sub")?.Value,
            scope = User.FindFirst("scope")?.Value,
            tokenUse = User.FindFirst("token_use")?.Value ?? "client_credentials",
        });
    }
}
