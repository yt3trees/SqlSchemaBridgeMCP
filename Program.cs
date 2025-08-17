using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Repositories;
using SqlSchemaBridgeMCP.Services;
using SqlSchemaBridgeMCP.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the application services.
builder.Services.AddSingleton<ProfileManager>();
builder.Services.AddSingleton<SchemaProvider>();
builder.Services.AddSingleton<SchemaEditorService>();
builder.Services.AddSingleton<CsvConverterService>();
builder.Services.AddSingleton<ProfileValidationService>();
builder.Services.AddSingleton<ISchemaRepository, SchemaRepository>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SqlSchemaBridgeTools>()
    .WithTools<SqlSchemaEditorTools>()
    .WithTools<ProfileValidationTools>()
    .WithTools<ProfileManagementTools>()
    .WithTools<SqlValidationTools>();

await builder.Build().RunAsync();
