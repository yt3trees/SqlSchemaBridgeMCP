using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SqlSchemaBridgeTools>()
    .WithTools<SqlSchemaEditorTools>();

await builder.Build().RunAsync();
