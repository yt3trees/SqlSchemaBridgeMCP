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
    .WithListResourcesHandler(async (ctx, ct) =>
    {
        return new ListResourcesResult
        {
            Resources =
            [
                new Resource
                {
                    Uri = "sql-schema://tables",
                    Name = "SQL Tables",
                    Description = "Database table definitions",
                    MimeType = "application/json"
                },
                new Resource
                {
                    Uri = "sql-schema://columns",
                    Name = "SQL Columns",
                    Description = "Database column definitions",
                    MimeType = "application/json"
                },
                new Resource
                {
                    Uri = "sql-schema://relations",
                    Name = "SQL Relations",
                    Description = "Database table relationships",
                    MimeType = "application/json"
                }
            ]
        };
    })
    .WithReadResourceHandler(async (ctx, ct) =>
    {
        var uri = ctx.Params?.Uri;
        var schemaProvider = ctx.Services.GetRequiredService<SchemaProvider>();

        switch (uri)
        {
            case "sql-schema://tables":
                var tables = schemaProvider.Tables;
                var tablesJson = System.Text.Json.JsonSerializer.Serialize(tables,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                return new ReadResourceResult
                {
                    Contents = [new TextResourceContents
                    {
                        Text = tablesJson,
                        MimeType = "application/json",
                        Uri = uri
                    }]
                };

            case "sql-schema://columns":
                var columns = schemaProvider.Columns;
                var columnsJson = System.Text.Json.JsonSerializer.Serialize(columns,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                return new ReadResourceResult
                {
                    Contents = [new TextResourceContents
                    {
                        Text = columnsJson,
                        MimeType = "application/json",
                        Uri = uri
                    }]
                };

            case "sql-schema://relations":
                var relations = schemaProvider.Relations;
                var relationsJson = System.Text.Json.JsonSerializer.Serialize(relations,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                return new ReadResourceResult
                {
                    Contents = [new TextResourceContents
                    {
                        Text = relationsJson,
                        MimeType = "application/json",
                        Uri = uri
                    }]
                };

            default:
                throw new NotSupportedException($"Unknown resource: {uri}");
        }
    });

await builder.Build().RunAsync();