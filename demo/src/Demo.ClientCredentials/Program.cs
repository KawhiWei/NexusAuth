using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var authority = Environment.GetEnvironmentVariable("NEXUSAUTH_AUTHORITY") ?? "http://localhost:5100";
var clientId = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_ID") ?? "demo-cc";
var clientSecret = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_SECRET") ?? "demo-bff-secret";
var scope = Environment.GetEnvironmentVariable("NEXUSAUTH_SCOPE") ?? "demo_api";
var apiUrl = Environment.GetEnvironmentVariable("DEMO_BFF_API") ?? "http://localhost:5201/api/m2m/profile";

Console.WriteLine("=== Demo: client_credentials ===");
Console.WriteLine($"Authority: {authority}");
Console.WriteLine($"ClientId : {clientId}");
Console.WriteLine($"Scope    : {scope}");
Console.WriteLine($"BFF API  : {apiUrl}");
Console.WriteLine();

using var http = new HttpClient();

var discovery = await GetDiscoveryAsync(http, authority);
var tokenEndpoint = GetStringProperty(discovery, "token_endpoint");

ApplyBasicAuth(http, clientId, clientSecret);
using var request = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "client_credentials",
    ["scope"] = scope,
});

var response = await http.PostAsync(tokenEndpoint, request);
var payload = await response.Content.ReadAsStringAsync();

Console.WriteLine($"HTTP {(int)response.StatusCode} {response.StatusCode}");
PrintJson(payload);

if (!response.IsSuccessStatusCode)
    return;

using var tokenDocument = JsonDocument.Parse(payload);
if (!tokenDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement)
    || accessTokenElement.ValueKind != JsonValueKind.String)
{
    Console.WriteLine("未从 token 响应中解析到 access_token，无法继续调用 Demo.Bff。\n");
    return;
}

var accessToken = accessTokenElement.GetString()!;
Console.WriteLine();
Console.WriteLine("开始使用 access_token 调用 Demo.Bff 接口...");

using var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
apiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
var apiResponse = await http.SendAsync(apiRequest);
var apiPayload = await apiResponse.Content.ReadAsStringAsync();

Console.WriteLine($"BFF HTTP {(int)apiResponse.StatusCode} {apiResponse.StatusCode}");
PrintJson(apiPayload);

static async Task<JsonElement> GetDiscoveryAsync(HttpClient http, string authority)
{
    var url = authority.TrimEnd('/') + "/.well-known/openid-configuration";
    var response = await http.GetAsync(url);
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadAsStringAsync();
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
}

static string GetStringProperty(JsonElement element, string name)
{
    if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        throw new InvalidOperationException($"Discovery missing '{name}'.");

    return value.GetString()!;
}

static void ApplyBasicAuth(HttpClient http, string clientId, string clientSecret)
{
    var raw = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
}

static void PrintJson(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        var pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(pretty);
    }
    catch
    {
        Console.WriteLine(json);
    }
}
