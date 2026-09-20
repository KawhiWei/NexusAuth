using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using NexusAuth.Workbench.Api.Controllers;

namespace NexusAuth.Workbench.Api;

public sealed class GatewayControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        feature.Controllers.Remove(typeof(AuthController).GetTypeInfo());
    }
}
