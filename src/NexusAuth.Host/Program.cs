using Luck.AutoDependencyInjection;
using NexusAuth.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication<AppWebModule>();

var app = builder.Build();

app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();
app.InitializeApplication();

app.Run();
