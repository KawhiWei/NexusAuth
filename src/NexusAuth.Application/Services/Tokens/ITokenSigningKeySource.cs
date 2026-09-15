namespace NexusAuth.Application.Services.Tokens;

public interface ITokenSigningKeySource
{
    string Name { get; }

    TokenSigningKeyMaterial Load(TokenSigningKeySourceContext context);
}

public sealed record TokenSigningKeySourceContext(
    TokenSigningKeyOptions Options,
    string ContentRootPath,
    bool IsDevelopment);
