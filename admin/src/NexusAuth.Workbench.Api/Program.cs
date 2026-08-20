using Luck.AutoDependencyInjection;
using Luck.AspNetCore.Extensions;
using NexusAuth.Persistence;
using NexusAuth.Workbench.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new SystemTextJsonConvert.DateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new SystemTextJsonConvert.DateTimeNullConverter());
        options.JsonSerializerOptions.Converters.Add(new SystemTextJsonConvert.DateTimeOffsetConverter());
        options.JsonSerializerOptions.Converters.Add(new SystemTextJsonConvert.DateTimeOffsetNullConverter());
    });
builder.Services.AddApplication<WorkbenchApiModule>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "NexusAuth Workbench API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexusAuth Workbench API v1"));

app.MapControllers();
app.InitializeApplication();

app.Run();
