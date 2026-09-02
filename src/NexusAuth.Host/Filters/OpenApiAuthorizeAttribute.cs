using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusAuth.Application.Services.OpenApi;

namespace NexusAuth.Host.Filters;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OpenApiAuthorizeAttribute(string targetType) : Attribute, IFilterFactory, IOrderedFilter
{
    public bool IsReusable => false;
    public int Order => int.MinValue + 100;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new OpenApiAuthorizationFilter(serviceProvider.GetRequiredService<IOpenApiCredentialService>(), targetType);

    private sealed class OpenApiAuthorizationFilter(IOpenApiCredentialService credentialService, string targetType)
        : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
            var authenticated = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && await credentialService.ValidateAsync(authorization[7..].Trim(), targetType, context.HttpContext.RequestAborted);
            if (authenticated) return;

            context.Result = new UnauthorizedObjectResult(new
            {
                error = "invalid_token",
                error_description = "A valid Open API bearer credential for this resource type is required."
            });
        }
    }
}
