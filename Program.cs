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

await builder.Build().RunAsync();