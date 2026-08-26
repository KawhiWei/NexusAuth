using Luck.Framework.Exceptions;

namespace NexusAuth.Application.Services.ApiResources;

public sealed class ApiResourceAlreadyExistsException(string resourceName)
    : BusinessException(
        "ApiResourceAlreadyExists",
        $"资源标识 '{resourceName}' 已存在，请使用其他标识。")
{
    public string ResourceName { get; } = resourceName;
}
