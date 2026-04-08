using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var authority = Environment.GetEnvironmentVariable("NEXUSAUTH_AUTHORITY") ?? "http://localhost:5100";
var clientId = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_ID") ?? "demo-device";
var clientSecret = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_SECRET") ?? "demo-bff-secret";
var scope = Environment.GetEnvironmentVariable("NEXUSAUTH_SCOPE") ?? "openid profile email phone offline_access demo_api";

Console.WriteLine("=== Demo: device_code ===");
Console.WriteLine($"Authority: {authority}");
Console.WriteLine($"ClientId : {clientId}");
Console.WriteLine($"Scope    : {scope}");
Console.WriteLine();

using var http = new HttpClient();
var discovery = await GetDiscoveryAsync(http, authority);
var deviceEndpoint = GetStringProperty(discovery, "device_authorization_endpoint");
var tokenEndpoint = GetStringProperty(discovery, "token_endpoint");

ApplyBasicAuth(http, clientId, clientSecret);

var startResponse = await http.PostAsync(deviceEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["scope"] = scope,
}));

var startPayload = await startResponse.Content.ReadAsStringAsync();
if (!startResponse.IsSuccessStatusCode)
{
    Console.WriteLine($"Start failed: {(int)startResponse.StatusCode}");
    PrintJson(startPayload);
    return;
}

using var startDocument = JsonDocument.Parse(startPayload);
var startRoot = startDocument.RootElement;
var deviceCode = GetStringProperty(startRoot, "device_code");
var userCode = GetStringProperty(startRoot, "user_code");
var verificationUri = GetStringProperty(startRoot, "verification_uri");
var verificationUriComplete = GetStringProperty(startRoot, "verification_uri_complete");
var interval = startRoot.TryGetProperty("interval", out var intervalElement) && intervalElement.TryGetInt32(out var intervalValue)
    ? Math.Max(intervalValue, 1)
    : 5;

Console.WriteLine("设备授权已启动：");
Console.WriteLine($"- user_code: {userCode}");
Console.WriteLine($"- verification_uri: {verificationUri}");
Console.WriteLine($"- verification_uri_complete: {verificationUriComplete}");
Console.WriteLine();
Console.WriteLine("请在浏览器完成登录与授权，然后回到当前终端等待轮询结果...\n");

while (true)
{
    await Task.Delay(TimeSpan.FromSeconds(interval));

    var pollResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
        ["device_code"] = deviceCode,
    }));

    var pollPayload = await pollResponse.Content.ReadAsStringAsync();
    using var pollDocument = JsonDocument.Parse(pollPayload);
    var pollRoot = pollDocument.RootElement;

    if (pollResponse.IsSuccessStatusCode)
    {
        Console.WriteLine("设备授权成功，token 响应如下：");
        PrintJson(pollPayload);
        if (pollRoot.TryGetProperty("refresh_token", out var refreshTokenElement) && refreshTokenElement.ValueKind == JsonValueKind.String)
        {
            Console.WriteLine();
            Console.WriteLine("可用于 Demo.RefreshToken 的 refresh_token:");
            Console.WriteLine(refreshTokenElement.GetString());
        }

        break;
    }

    var error = pollRoot.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String
        ? errorElement.GetString()
        : "unknown_error";

    if (string.Equals(error, "authorization_pending", StringComparison.Ordinal))
    {
        Console.WriteLine("授权尚未完成，继续轮询...");
        continue;
    }

    if (string.Equals(error, "slow_down", StringComparison.Ordinal))
    {
        interval += 2;
        Console.WriteLine($"服务端要求降频，新的轮询间隔: {interval}s");
        continue;
    }

    Console.WriteLine("设备授权失败：");
    PrintJson(pollPayload);
    break;
}

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
        throw new InvalidOperationException($"JSON missing '{name}'.");

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
