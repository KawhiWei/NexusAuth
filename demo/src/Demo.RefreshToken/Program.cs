using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var authority = Environment.GetEnvironmentVariable("NEXUSAUTH_AUTHORITY") ?? "http://localhost:5100";
var clientId = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_ID") ?? "demo-device";
var clientSecret = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_SECRET") ?? "demo-bff-secret";

Console.WriteLine("=== Demo: refresh_token ===");
Console.WriteLine("请先通过 Demo.DeviceCode 拿到 refresh_token，再粘贴到这里。\n");

var refreshToken = Environment.GetEnvironmentVariable("NEXUSAUTH_REFRESH_TOKEN");
if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.Write("Refresh Token: ");
    refreshToken = Console.ReadLine();
}

if (string.IsNullOrWhiteSpace(refreshToken))
{
    Console.WriteLine("未提供 refresh_token，退出。\n");
    return;
}

using var http = new HttpClient();
var discovery = await GetDiscoveryAsync(http, authority);
var tokenEndpoint = GetStringProperty(discovery, "token_endpoint");

ApplyBasicAuth(http, clientId, clientSecret);
using var request = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "refresh_token",
    ["refresh_token"] = refreshToken,
});

var response = await http.PostAsync(tokenEndpoint, request);
var payload = await response.Content.ReadAsStringAsync();

Console.WriteLine($"HTTP {(int)response.StatusCode} {response.StatusCode}");
PrintJson(payload);

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
