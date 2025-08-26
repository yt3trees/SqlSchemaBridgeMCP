using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

/// <summary>
/// Provides tools for editing the database schema CSV files.
/// </summary>
public class SqlSchemaEditorTools
{
    private readonly SchemaEditorService _editorService;
    private readonly ILogger<SqlSchemaEditorTools> _logger;

    public SqlSchemaEditorTools(SchemaEditorService editorService, ILogger<SqlSchemaEditorTools> logger)
    {
        _editorService = editorService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Manages schema elements (tables, columns, relations) with add/delete operations.")]
    public string SqlSchemaManageSchema(
        [Description("The operation to perform: 'add' or 'delete'.")] string operation,
        [Description("The type of element: 'table', 'column', or 'relation'.")] string elementType,
        [Description("The logical name (for tables/columns).")] string? logicalName = null,
        [Description("The physical name (for tables/columns) or physical name of the table (for columns).")] string? physicalName = null,
        [Description("The primary key (for tables) or data type (for columns).")] string? primaryKeyOrDataType = null,
        [Description("Description of the element.")] string? description = null,
        [Description("Database name (for tables).")] string? databaseName = null,
        [Description("Schema name (for tables).")] string? schemaName = null,
        [Description("Table physical name (for columns) or source table (for relations).")] string? tablePhysicalNameOrSourceTable = null,
        [Description("Source column (for relations).")] string? sourceColumn = null,
        [Description("Target table (for relations).")] string? targetTable = null,
        [Description("Target column (for relations).")] string? targetColumn = null)
    {
        var op = operation.ToLowerInvariant();
        var type = elementType.ToLowerInvariant();

        switch (type)
        {
            case "table":
                return op == "add"
                    ? AddTableInternal(logicalName!, physicalName!, primaryKeyOrDataType!, description, databaseName, schemaName)
                    : DeleteTableInternal(physicalName!);

            case "column":
                return op == "add"
                    ? AddColumnInternal(tablePhysicalNameOrSourceTable!, logicalName!, physicalName!, primaryKeyOrDataType!, description)
                    : DeleteColumnInternal(tablePhysicalNameOrSourceTable!, physicalName!);

            case "relation":
                return op == "add"
                    ? AddRelationInternal(tablePhysicalNameOrSourceTable!, sourceColumn!, targetTable!, targetColumn!)
                    : DeleteRelationInternal(tablePhysicalNameOrSourceTable!, sourceColumn!, targetTable!, targetColumn!);

            default:
                return $"Invalid element type '{elementType}'. Must be 'table', 'column', or 'relation'.";
        }
    }

    private string AddTableInternal(string logicalName, string physicalName, string primaryKey, string? description, string? databaseName, string? schemaName)
    {
        _logger.LogInformation("Executing AddTable tool for {PhysicalName}", physicalName);
        var table = new Table { DatabaseName = databaseName, SchemaName = schemaName, LogicalName = logicalName, PhysicalName = physicalName, PrimaryKey = primaryKey, Description = description };
        _editorService.AddRecord(table, "tables.csv");
        return $"Successfully added table '{physicalName}'.";
    }

    private string DeleteTableInternal(string physicalName)
    {
        _logger.LogInformation("Executing DeleteTable tool for {PhysicalName}", physicalName);
        _editorService.DeleteRecords<Table>(t => t.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase), "tables.csv");
        return $"Successfully deleted table '{physicalName}'.";
    }

    private string AddColumnInternal(string tablePhysicalName, string logicalName, string physicalName, string dataType, string? description)
    {
        _logger.LogInformation("Executing AddColumn tool for {TablePhysicalName}.{PhysicalName}", tablePhysicalName, physicalName);
        var column = new Column { TablePhysicalName = tablePhysicalName, LogicalName = logicalName, PhysicalName = physicalName, DataType = dataType, Description = description };
        _editorService.AddRecord(column, "columns.csv");
        return $"Successfully added column '{physicalName}' to table '{tablePhysicalName}'.";
    }

    private string DeleteColumnInternal(string tablePhysicalName, string physicalName)
    {
        _logger.LogInformation("Executing DeleteColumn tool for {TablePhysicalName}.{PhysicalName}", tablePhysicalName, physicalName);
        _editorService.DeleteRecords<Column>(c =>
            c.TablePhysicalName.Equals(tablePhysicalName, StringComparison.OrdinalIgnoreCase) &&
            c.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase),
            "columns.csv");
        return $"Successfully deleted column '{physicalName}' from table '{tablePhysicalName}'.";
    }

    private string AddRelationInternal(string sourceTable, string sourceColumn, string targetTable, string targetColumn)
    {
        _logger.LogInformation("Executing AddRelation tool for {SourceTable} -> {TargetTable}", sourceTable, targetTable);
        var relation = new Relation { SourceTable = sourceTable, SourceColumn = sourceColumn, TargetTable = targetTable, TargetColumn = targetColumn };
        _editorService.AddRecord(relation, "relations.csv");
        return $"Successfully added relation between '{sourceTable}' and '{targetTable}'.";
    }

    private string DeleteRelationInternal(string sourceTable, string sourceColumn, string targetTable, string targetColumn)
    {
        _logger.LogInformation("Executing DeleteRelation tool for {SourceTable} -> {TargetTable}", sourceTable, targetTable);
        _editorService.DeleteRecords<Relation>(r =>
            r.SourceTable.Equals(sourceTable, StringComparison.OrdinalIgnoreCase) &&
            r.SourceColumn.Equals(sourceColumn, StringComparison.OrdinalIgnoreCase) &&
            r.TargetTable.Equals(targetTable, StringComparison.OrdinalIgnoreCase) &&
            r.TargetColumn.Equals(targetColumn, StringComparison.OrdinalIgnoreCase),
            "relations.csv");
        return $"Successfully deleted relation between '{sourceTable}' and '{targetTable}'.";
    }
}
