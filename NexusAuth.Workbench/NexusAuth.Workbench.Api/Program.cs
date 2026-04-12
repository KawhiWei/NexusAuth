using Luck.AutoDependencyInjection;
using NexusAuth.Persistence;
using NexusAuth.Workbench.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApplication<WorkbenchApiModule>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NexusAuth Workbench API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexusAuth Workbench API v1"));

app.UseAuthorization();

app.MapControllers();
app.InitializeApplication();

app.Run();