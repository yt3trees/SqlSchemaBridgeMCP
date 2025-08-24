using Microsoft.Extensions.DependencyInjection;
using SqlSchemaBridgeMCP.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace SqlSchemaBridgeMCP.Resources;

public static class ResourceHandlers
{
    public static async ValueTask<ListResourcesResult> HandleListResources(RequestContext<ListResourcesRequestParams> ctx, CancellationToken ct)
    {
        var resources = new List<Resource>
        {
            new Resource
            {
                Uri = "sql-schema://mermaid-er-diagram",
                Name = "Mermaid ER Diagram",
                Description = "ER diagram of database schema in Mermaid format. Use 'sql-schema://mermaid-er-diagram' for all tables, or 'sql-schema://mermaid-er-diagram/{table_name}' for specific table and its relations.",
                MimeType = "text/vnd.mermaid"
            }
        };

        return new ListResourcesResult { Resources = resources };
    }

    public static async ValueTask<ReadResourceResult> HandleReadResource(RequestContext<ReadResourceRequestParams> ctx, CancellationToken ct)
    {
        var uri = ctx.Params?.Uri;
        if (uri == null)
        {
            throw new NotSupportedException("Resource URI cannot be null.");
        }

        var schemaProvider = ctx.Services.GetRequiredService<SchemaProvider>();
        var allTables = schemaProvider.Tables;
        var allColumns = schemaProvider.Columns;
        var allRelations = schemaProvider.Relations;

        List<Models.Table> targetTables;
        List<Models.Relation> targetRelations;

        const string baseUri = "sql-schema://mermaid-er-diagram";

        if (uri.StartsWith(baseUri + "/"))
        {
            var tableName = uri.Substring((baseUri + "/").Length);
            if (allTables.All(t => t.PhysicalName != tableName))
            {
                throw new NotSupportedException($"Table '{tableName}' not found in schema.");
            }

            var relatedTables = GetRelatedTables(tableName, allRelations);
            targetTables = allTables.Where(t => relatedTables.Contains(t.PhysicalName)).ToList();
            targetRelations = allRelations.Where(r => relatedTables.Contains(r.SourceTable) && relatedTables.Contains(r.TargetTable)).ToList();
        }
        else if (uri == baseUri)
        {
            targetTables = allTables.ToList();
            targetRelations = allRelations.ToList();
        }
        else
        {
            throw new NotSupportedException($"Unknown resource: {uri}");
        }

        var mermaidDiagram = GenerateMermaidDiagram(targetTables, allColumns, targetRelations);

        return new ReadResourceResult
        {
            Contents = [new TextResourceContents
            {
                Text = mermaidDiagram,
                MimeType = "text/vnd.mermaid",
                Uri = uri
            }]
        };
    }

    private static HashSet<string> GetRelatedTables(string rootTableName, IEnumerable<Models.Relation> allRelations)
    {
        var relatedTables = new HashSet<string>();

        relatedTables.Add(rootTableName);

        // Get only directly related tables (no recursion)
        var relations = allRelations.Where(r => r.SourceTable == rootTableName || r.TargetTable == rootTableName);

        foreach (var relation in relations)
        {
            var otherTable = relation.SourceTable == rootTableName ? relation.TargetTable : relation.SourceTable;
            relatedTables.Add(otherTable);
        }

        return relatedTables;
    }

    private static string GenerateMermaidDiagram(IEnumerable<Models.Table> tables, IEnumerable<Models.Column> allColumns, IEnumerable<Models.Relation> relations)
    {
        var mermaidDiagram = new StringBuilder();
        mermaidDiagram.AppendLine("erDiagram");

        var tableNames = new HashSet<string>(tables.Select(t => t.PhysicalName));
        var foreignKeys = new HashSet<string>(
            relations.Select(r => $"{r.SourceTable}.{r.SourceColumn}")
                     .Concat(relations.Select(r => $"{r.TargetTable}.{r.TargetColumn}")));

        foreach (var table in tables)
        {
            mermaidDiagram.AppendLine($"    {table.PhysicalName} {{");
            var tableColumns = allColumns.Where(c => c.TablePhysicalName == table.PhysicalName);
            foreach (var column in tableColumns)
            {
                var pk = table.PrimaryKey == column.PhysicalName ? " PK" : "";
                var fk = foreignKeys.Contains($"{table.PhysicalName}.{column.PhysicalName}") ? " FK" : "";

                // Simplify data type
                var dataType = SimplifyDataType(column.DataType);

                // Process description line breaks and quotes
                var description = "";
                if (!string.IsNullOrWhiteSpace(column.Description))
                {
                    var cleanDescription = column.Description
                        .Replace("\r\n", " ")
                        .Replace("\n", " ")
                        .Replace("\r", " ")
                        .Replace("\"", "'")
                        .Trim();
                    description = $" \"{cleanDescription}\"";
                }

                mermaidDiagram.AppendLine($"        {dataType} {column.PhysicalName}{pk}{fk}{description}");
            }
                mermaidDiagram.AppendLine("}");
        }

        foreach (var relation in relations)
        {
            mermaidDiagram.AppendLine($"    {relation.SourceTable} ||--o{{ {relation.TargetTable} : \"\" ");
        }

        return mermaidDiagram.ToString();
    }

    private static string SimplifyDataType(string dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return "string";

        var upperType = dataType.ToUpper();

        // String types
        if (upperType.Contains("NVARCHAR") || upperType.Contains("VARCHAR") || 
            upperType.Contains("NCHAR") || upperType.Contains("CHAR") ||
            upperType.Contains("NTEXT") || upperType.Contains("TEXT"))
            return "string";

        // Date/time types
        if (upperType.Contains("DATETIME") || upperType.Contains("DATE") || 
            upperType.Contains("TIME") || upperType.Contains("TIMESTAMP"))
            return "datetime";

        // Binary types
        if (upperType.Contains("VARBINARY") || upperType.Contains("BINARY") ||
            upperType.Contains("IMAGE"))
            return "binary";

        // Numeric types
        if (upperType.Contains("INT") || upperType.Contains("BIGINT") ||
            upperType.Contains("SMALLINT") || upperType.Contains("TINYINT"))
            return "int";

        if (upperType.Contains("DECIMAL") || upperType.Contains("NUMERIC") ||
            upperType.Contains("FLOAT") || upperType.Contains("REAL") ||
            upperType.Contains("MONEY"))
            return "decimal";

        // GUID types
        if (upperType.Contains("UNIQUEIDENTIFIER") || upperType.Contains("GUID"))
            return "string";

        // Boolean types
        if (upperType.Contains("BIT") || upperType.Contains("BOOLEAN"))
            return "boolean";

        // Treat other types as string
        return "string";
    }
}
