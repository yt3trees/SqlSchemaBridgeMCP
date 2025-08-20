using Microsoft.Extensions.DependencyInjection;
using SqlSchemaBridgeMCP.Services;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace SqlSchemaBridgeMCP.Resources;

public static class ResourceHandlers
{
    public static async ValueTask<ListResourcesResult> HandleListResources(RequestContext<ListResourcesRequestParams> ctx, CancellationToken ct)
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
    }

    public static async ValueTask<ReadResourceResult> HandleReadResource(RequestContext<ReadResourceRequestParams> ctx, CancellationToken ct)
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
    }
}