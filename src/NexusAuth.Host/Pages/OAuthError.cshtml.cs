using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NexusAuth.Host.Pages;

public sealed class OAuthErrorModel : PageModel
{
    [BindProperty(SupportsGet = true, Name = "error")]
    public string Error { get; set; } = "invalid_request";

    [BindProperty(SupportsGet = true, Name = "error_description")]
    public string Description { get; set; } = "授权请求无效或不再可用。";
}
