using System.Collections.Concurrent;
using Demo.Bff.Models;
using Demo.Bff.Options;
using Demo.Bff.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var nexusAuthSection = builder.Configuration.GetSection("NexusAuth");
var authority = nexusAuthSection["Authority"]!;

var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"]!;
var audience = jwtSection["Audience"]!;
var sessionCookieName = builder.Configuration["Session:CookieName"] ?? ".Demo.Bff.Session";
var sessionCookieMinutes = int.TryParse(builder.Configuration["Session:CookieLifetimeMinutes"], out var parsedSessionCookieMinutes)
    ? parsedSessionCookieMinutes
    : 10;

builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection("Frontend"));
builder.Services.Configure<NexusAuthBffOptions>(builder.Configuration.GetSection("NexusAuth"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = sessionCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionCookieMinutes);
        options.EventsType = typeof(OidcSessionCookieEvents);
    })
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = audience;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    });

builder.Services.PostConfigureAll<JwtBearerOptions>(options =>
{
    options.TokenValidationParameters.ValidateAudience = true;
    options.TokenValidationParameters.ValidAudience = null;
    options.TokenValidationParameters.ValidAudiences = new[]
    {
        "demo-bff-api",
        builder.Configuration["Jwt:Audience"] ?? "NexusAuth",
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddSingleton<ConcurrentDictionary<string, OidcFlowState>>();
builder.Services.AddScoped<OidcBffService>();
builder.Services.AddScoped<OidcSessionCookieEvents>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
