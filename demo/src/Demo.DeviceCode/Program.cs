using System.Net.Http.Headers;
using System.Text.Json;

var authority = Environment.GetEnvironmentVariable("NEXUSAUTH_AUTHORITY") ?? "http://localhost:5100";
var clientId = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_ID") ?? "demo-device";
var privateKeyPath = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_PRIVATE_KEY_PATH") ?? Path.Combine(AppContext.BaseDirectory, "keys", "demo-client-private.pem");
var keyId = Environment.GetEnvironmentVariable("NEXUSAUTH_CLIENT_KEY_ID") ?? "demo-device-key-1";
var scope = Environment.GetEnvironmentVariable("NEXUSAUTH_SCOPE") ?? "openid profile email phone offline_access demo_api";
var apiUrl = Environment.GetEnvironmentVariable("DEMO_BFF_API") ?? "http://localhost:5201/api/m2m/profile";

Console.WriteLine("=== Demo: device_code ===");
Console.WriteLine($"Authority: {authority}");
Console.WriteLine($"ClientId : {clientId}");
Console.WriteLine($"Auth     : private_key_jwt ({keyId})");
Console.WriteLine($"Scope    : {scope}");
Console.WriteLine($"BFF API  : {apiUrl}");
Console.WriteLine();

using var http = new HttpClient();
var discovery = await GetDiscoveryAsync(http, authority);
var deviceEndpoint = GetStringProperty(discovery, "device_authorization_endpoint");
var tokenEndpoint = GetStringProperty(discovery, "token_endpoint");

var startForm = new Dictionary<string, string>
{
    ["scope"] = scope,
};
await ClientAssertionHelper.AppendPrivateKeyJwtAsync(startForm, clientId, deviceEndpoint, privateKeyPath, keyId);

var startResponse = await http.PostAsync(deviceEndpoint, new FormUrlEncodedContent(startForm));

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

    var pollForm = new Dictionary<string, string>
    {
        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
        ["device_code"] = deviceCode,
    };
    await ClientAssertionHelper.AppendPrivateKeyJwtAsync(pollForm, clientId, tokenEndpoint, privateKeyPath, keyId);

    var pollResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(pollForm));

    var pollPayload = await pollResponse.Content.ReadAsStringAsync();
    using var pollDocument = JsonDocument.Parse(pollPayload);
    var pollRoot = pollDocument.RootElement;

    if (pollResponse.IsSuccessStatusCode)
    {
        Console.WriteLine("设备授权成功，token 响应如下：");
        PrintJson(pollPayload);

        if (pollRoot.TryGetProperty("access_token", out var accessTokenElement) && accessTokenElement.ValueKind == JsonValueKind.String)
        {
            var accessToken = accessTokenElement.GetString()!;
            Console.WriteLine();
            Console.WriteLine("开始使用 access_token 调用 Demo.Bff 接口...");

            using var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            apiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var apiResponse = await http.SendAsync(apiRequest);
            var apiPayload = await apiResponse.Content.ReadAsStringAsync();

            Console.WriteLine($"BFF HTTP {(int)apiResponse.StatusCode} {apiResponse.StatusCode}");
            PrintJson(apiPayload);

        if (pollRoot.TryGetProperty("refresh_token", out var issuedRefreshTokenElement) && issuedRefreshTokenElement.ValueKind == JsonValueKind.String)
        {
            var refreshToken = issuedRefreshTokenElement.GetString()!;

                Console.WriteLine();
                Console.WriteLine("开始自动验证 refresh_token 流程...");

                var refreshForm = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                };
                await ClientAssertionHelper.AppendPrivateKeyJwtAsync(refreshForm, clientId, tokenEndpoint, privateKeyPath, keyId);

                var refreshResponse = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(refreshForm));
                var refreshPayload = await refreshResponse.Content.ReadAsStringAsync();

                Console.WriteLine($"Refresh HTTP {(int)refreshResponse.StatusCode} {refreshResponse.StatusCode}");
                PrintJson(refreshPayload);

                if (refreshResponse.IsSuccessStatusCode)
                {
                    using var refreshDocument = JsonDocument.Parse(refreshPayload);
                    var refreshRoot = refreshDocument.RootElement;
                    if (refreshRoot.TryGetProperty("access_token", out var refreshedAccessTokenElement) && refreshedAccessTokenElement.ValueKind == JsonValueKind.String)
                    {
                        Console.WriteLine();
                        Console.WriteLine("开始使用刷新后的 access_token 再次调用 Demo.Bff 接口...");

                        using var refreshedApiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                        refreshedApiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAccessTokenElement.GetString());
                        var refreshedApiResponse = await http.SendAsync(refreshedApiRequest);
                        var refreshedApiPayload = await refreshedApiResponse.Content.ReadAsStringAsync();

                        Console.WriteLine($"Refreshed BFF HTTP {(int)refreshedApiResponse.StatusCode} {refreshedApiResponse.StatusCode}");
                        PrintJson(refreshedApiPayload);
                    }
                }
            }
        }

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
