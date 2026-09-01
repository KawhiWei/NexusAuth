
namespace NexusAuth.Application.Services.OIDC;

public sealed class OidcRequestedClaims
{
    public static readonly OidcRequestedClaims Empty = new(
        new Dictionary<string, OidcClaimRequest>(StringComparer.Ordinal),
        new Dictionary<string, OidcClaimRequest>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, OidcClaimRequest> IdTokenClaimRequests { get; }

    public IReadOnlyDictionary<string, OidcClaimRequest> UserInfoClaimRequests { get; }

    public HashSet<string> IdTokenClaims => IdTokenClaimRequests.Keys.ToHashSet(StringComparer.Ordinal);

    public HashSet<string> UserInfoClaims => UserInfoClaimRequests.Keys.ToHashSet(StringComparer.Ordinal);

    public bool HasExplicitRequests => IdTokenClaims.Count > 0 || UserInfoClaims.Count > 0;

    private OidcRequestedClaims(
        IReadOnlyDictionary<string, OidcClaimRequest> idTokenClaimRequests,
        IReadOnlyDictionary<string, OidcClaimRequest> userInfoClaimRequests)
    {
        IdTokenClaimRequests = idTokenClaimRequests;
        UserInfoClaimRequests = userInfoClaimRequests;
    }

    /// <summary>
    /// </summary>
    public static OidcRequestedClaims Create(IEnumerable<string>? idTokenClaims = null, IEnumerable<string>? userInfoClaims = null)
    {
        var idTokenMap = (idTokenClaims ?? [])
            .ToDictionary(claim => claim, _ => OidcClaimRequest.Empty, StringComparer.Ordinal);
        var userInfoMap = (userInfoClaims ?? [])
            .ToDictionary(claim => claim, _ => OidcClaimRequest.Empty, StringComparer.Ordinal);

        return new OidcRequestedClaims(
            idTokenMap,
            userInfoMap);
    }

    /// <summary>
    /// </summary>
    public static OidcRequestedClaims Parse(string? claimsJson)
    {
        if (string.IsNullOrWhiteSpace(claimsJson))
            return Empty;

        using var document = JsonDocument.Parse(claimsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("OIDC claims parameter must be a JSON object.");

        return new OidcRequestedClaims(
            ParseClaimSection(document.RootElement, "id_token"),
            ParseClaimSection(document.RootElement, "userinfo"));
    }

    /// <summary>
    /// </summary>
    public bool RequestsIdTokenClaim(string claimName)
    {
        return IdTokenClaimRequests.ContainsKey(claimName);
    }

    /// <summary>
    /// </summary>
    public bool RequestsUserInfoClaim(string claimName)
    {
        return UserInfoClaimRequests.ContainsKey(claimName);
    }

    /// <summary>
    /// </summary>
    public OidcClaimRequest? GetClaimRequest(string claimName)
    {
        if (IdTokenClaimRequests.TryGetValue(claimName, out var idTokenRequest))
            return idTokenRequest;

        return UserInfoClaimRequests.TryGetValue(claimName, out var userInfoRequest) ? userInfoRequest : null;
    }

    /// <summary>
    /// </summary>
    public bool RequestsClaim(string claimName)
    {
        return RequestsIdTokenClaim(claimName) || RequestsUserInfoClaim(claimName);
    }

    private static IReadOnlyDictionary<string, OidcClaimRequest> ParseClaimSection(JsonElement root, string sectionName)
    {
        var claims = new Dictionary<string, OidcClaimRequest>(StringComparer.Ordinal);
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
            return claims;

        foreach (var property in section.EnumerateObject())
        {
            claims[property.Name] = OidcClaimRequest.Parse(property.Value);
        }

        return claims;
    }
}

public sealed record OidcClaimRequest(bool? Essential, string? Value, IReadOnlyList<string> Values)
{
    public static readonly OidcClaimRequest Empty = new(null, null, []);

    public static OidcClaimRequest Parse(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return Empty;

        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("OIDC claim request entries must be objects or null.");

        bool? essential = null;
        string? value = null;
        List<string> values = [];

        if (element.TryGetProperty("essential", out var essentialElement))
        {
            if (essentialElement.ValueKind != JsonValueKind.True && essentialElement.ValueKind != JsonValueKind.False)
                throw new InvalidOperationException("OIDC claim request 'essential' must be a boolean.");

            essential = essentialElement.GetBoolean();
        }

        if (element.TryGetProperty("value", out var valueElement))
        {
            if (valueElement.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("OIDC claim request 'value' must be a string.");

            value = valueElement.GetString();
        }

        if (element.TryGetProperty("values", out var valuesElement))
        {
            if (valuesElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("OIDC claim request 'values' must be an array.");

            values = [.. valuesElement.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()];
        }

        return new OidcClaimRequest(essential, value, values);
    }

    public bool Matches(string? actualValue)
    {
        if (string.IsNullOrWhiteSpace(actualValue))
            return !IsConstrained;

        if (!string.IsNullOrWhiteSpace(Value) && !string.Equals(actualValue, Value, StringComparison.Ordinal))
            return false;

        if (Values.Count > 0 && !Values.Contains(actualValue, StringComparer.Ordinal))
            return false;

        return true;
    }

    public bool IsConstrained => !string.IsNullOrWhiteSpace(Value) || Values.Count > 0;
}
