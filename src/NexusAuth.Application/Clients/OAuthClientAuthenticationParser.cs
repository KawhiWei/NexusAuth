namespace NexusAuth.Application.Clients;

public static class OAuthClientAuthenticationParser
{
    /// <summary>
    /// Parses the client authentication methods defined by RFC 6749 and RFC 7523.
    /// A malformed or ambiguous request is a failed authentication, never a fallback
    /// to a different method.
    /// </summary>
    public static ClientAuthenticationParseResult ResolveClientAuthentication(
        string? authorizationHeader,
        string? formClientId,
        string? formClientSecret,
        string? formClientAssertionType = null,
        string? formClientAssertion = null,
        string? assertionAudience = null)
    {
        var hasFormSecret = formClientSecret is not null;
        var hasFormAssertionType = formClientAssertionType is not null;
        var hasFormAssertion = formClientAssertion is not null;
        var hasFormAssertionData = hasFormAssertionType || hasFormAssertion;

        if (hasFormSecret && hasFormAssertionData)
            return ClientAuthenticationParseResult.Failure("Multiple client authentication methods were supplied.");

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return ClientAuthenticationParseResult.Success(new ClientAuthenticationInput(
                formClientId,
                formClientSecret,
                formClientAssertionType,
                formClientAssertion,
                assertionAudience,
                hasFormAssertionData
                    ? null
                    : hasFormSecret
                        ? OAuthClient.TokenEndpointAuthMethodClientSecretPost
                        : null));
        }

        var header = authorizationHeader.Trim();
        var separator = header.IndexOfAny([' ', '\t']);
        if (separator <= 0)
            return ClientAuthenticationParseResult.Failure("Authorization header is malformed.");

        var scheme = header[..separator];
        if (!string.Equals(scheme, "Basic", StringComparison.OrdinalIgnoreCase))
            return ClientAuthenticationParseResult.Failure("Unsupported client authentication scheme.");

        var encodedCredentials = header[(separator + 1)..].Trim();
        if (encodedCredentials.Length == 0 || encodedCredentials.Any(char.IsWhiteSpace))
            return ClientAuthenticationParseResult.Failure("Authorization header is malformed.");

        if (hasFormSecret || hasFormAssertionData)
            return ClientAuthenticationParseResult.Failure("Multiple client authentication methods were supplied.");

        string decoded;
        try
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(Convert.FromBase64String(encodedCredentials));
        }
        catch (FormatException)
        {
            return ClientAuthenticationParseResult.Failure("Authorization header is malformed.");
        }
        catch (DecoderFallbackException)
        {
            return ClientAuthenticationParseResult.Failure("Authorization header is malformed.");
        }

        var credentialSeparator = decoded.IndexOf(':');
        if (credentialSeparator <= 0)
            return ClientAuthenticationParseResult.Failure("Authorization header is malformed.");

        if (!TryDecodeFormComponent(decoded[..credentialSeparator], out var headerClientId)
            || !TryDecodeFormComponent(decoded[(credentialSeparator + 1)..], out var headerClientSecret)
            || string.IsNullOrWhiteSpace(headerClientId)
            || string.IsNullOrWhiteSpace(headerClientSecret))
        {
            return ClientAuthenticationParseResult.Failure("Authorization header is malformed.");
        }

        if (formClientId is not null
            && !string.Equals(formClientId, headerClientId, StringComparison.Ordinal))
        {
            return ClientAuthenticationParseResult.Failure("Conflicting client_id values were supplied.");
        }

        return ClientAuthenticationParseResult.Success(new ClientAuthenticationInput(
            headerClientId,
            headerClientSecret,
            null,
            null,
            assertionAudience,
            OAuthClient.TokenEndpointAuthMethodClientSecretBasic));
    }

    private static bool TryDecodeFormComponent(string value, out string decoded)
    {
        decoded = string.Empty;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
                continue;

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        try
        {
            decoded = Uri.UnescapeDataString(value.Replace('+', ' '));
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
