using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using SqlSchemaBridgeMCP.Repositories;
using SqlSchemaBridgeMCP.Services;
using SqlSchemaBridgeMCP.Tools;
using ModelContextProtocol.Protocol;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<ProfileManager>();
builder.Services.AddSingleton<SchemaProvider>();
builder.Services.AddSingleton<SchemaEditorService>();
builder.Services.AddSingleton<CsvConverterService>();
builder.Services.AddSingleton<ProfileValidationService>();
builder.Services.AddSingleton<ISchemaRepository, SchemaRepository>();
builder.Services.AddSingleton<SqlSchemaBridgeTools>();
builder.Services.AddSingleton<SqlSchemaEditorTools>();
builder.Services.AddSingleton<WebDebugService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SqlSchemaBridgeTools>()
    .WithTools<SqlSchemaEditorTools>()
    .WithTools<ProfileValidationTools>()
    .WithTools<ProfileManagementTools>()
    .WithTools<SqlValidationTools>()
    .WithListResourcesHandler(SqlSchemaBridgeMCP.Resources.ResourceHandlers.HandleListResources)
    .WithReadResourceHandler(SqlSchemaBridgeMCP.Resources.ResourceHandlers.HandleReadResource);

var app = builder.Build();

// Configure and start web debug interface based on configuration
var webConfig = WebDebugConfiguration.FromCommandLineArgs(args);

if (webConfig.EnableWebDebugInterface && webConfig.AutoStartWithMCP)
{
    try
    {
        var webDebugService = app.Services.GetRequiredService<WebDebugService>();
        webDebugService.StartInBackground();
        
        // Give the web server a moment to start and report its URL
        await Task.Delay(1000);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to start web debug interface");
        Console.Error.WriteLine($"Warning: Web debug interface failed to start: {ex.Message}");
    }
}

await app.RunAsync();