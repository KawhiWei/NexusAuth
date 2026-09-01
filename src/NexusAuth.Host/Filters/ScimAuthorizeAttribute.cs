using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusAuth.Application.Services.Scim;

namespace NexusAuth.Host.Filters;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ScimAuthorizeAttribute(string requiredScope, int failureStatusCode = StatusCodes.Status401Unauthorized)
    : Attribute, IFilterFactory, IOrderedFilter
{
    public bool IsReusable => false;

    public int Order => int.MinValue + 100;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var credentialService = serviceProvider.GetRequiredService<IScimCredentialService>();
        return new ScimAuthorizationFilter(credentialService, requiredScope, failureStatusCode);
    }

    private sealed class ScimAuthorizationFilter(IScimCredentialService credentialService,
        string requiredScope, int failureStatusCode) : IAsyncAuthorizationFilter
    {
        private const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";
        private const string ScimContentType = "application/scim+json";

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
            var authenticated = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && await credentialService.ValidateAsync(
                    authorization[7..].Trim(),
                    requiredScope,
                    context.HttpContext.RequestAborted);

            if (authenticated)
                return;

            var forbidden = failureStatusCode == StatusCodes.Status403Forbidden;
            context.Result = new JsonResult(new
            {
                schemas = new[] { ErrorSchema },
                status = failureStatusCode.ToString(),
                scimType = forbidden ? "forbidden" : "invalidToken",
                detail = forbidden
                    ? "The credential does not grant SCIM write access."
                    : "A valid SCIM bearer token is required."
            })
            {
                StatusCode = failureStatusCode,
                ContentType = ScimContentType
            };
        }
    }
}
