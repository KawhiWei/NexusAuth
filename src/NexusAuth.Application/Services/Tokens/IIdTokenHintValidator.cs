namespace NexusAuth.Application.Services.Tokens;

public interface IIdTokenHintValidator
{
    IdTokenHintValidationResult Validate(string idTokenHint);
}
