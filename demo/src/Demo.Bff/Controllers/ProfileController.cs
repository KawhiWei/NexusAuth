using Demo.Bff.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Bff.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class ProfileController : ControllerBase
{
    private readonly OidcBffService _oidcBffService;

    public ProfileController(OidcBffService oidcBffService)
    {
        _oidcBffService = oidcBffService;
    }

    [HttpGet("/api/profile")]
    public IActionResult Profile()
    {
        var session = _oidcBffService.ReadSession(User);
        if (session is null)
            return Unauthorized();

        return Ok(new
        {
            message = "This is a protected business API behind the BFF.",
            user = session.User,
            tokenType = "server-session",
        });
    }
}
