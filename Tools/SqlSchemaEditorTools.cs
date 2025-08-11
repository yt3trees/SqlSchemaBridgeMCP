using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

/// <summary>
/// Provides tools for editing the database schema CSV files.
/// </summary>
internal class SqlSchemaEditorTools
{
    private readonly SchemaEditorService _editorService;
    private readonly ILogger<SqlSchemaEditorTools> _logger;

    public SqlSchemaEditorTools(SchemaEditorService editorService, ILogger<SqlSchemaEditorTools> logger)
    {
        _editorService = editorService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Adds a new table definition to tables.csv.")]
    public string AddTable(
        [Description("The logical name of the table (e.g., 'Customers').")] string logicalName,
        [Description("The physical name of the table (e.g., 'M_CUSTOMERS').")] string physicalName,
        [Description("The primary key of the table (e.g., 'CUSTOMER_ID').")] string primaryKey,
        [Description("A description of the table.")] string description,
        [Description("The physical name of the database.")] string? databaseName = null,
        [Description("The physical name of the schema.")] string? schemaName = null)
    {
        _logger.LogInformation("Executing AddTable tool for {PhysicalName}", physicalName);
        var table = new Table { DatabaseName = databaseName, SchemaName = schemaName, LogicalName = logicalName, PhysicalName = physicalName, PrimaryKey = primaryKey, Description = description };
        _editorService.AddRecord(table, "tables.csv");
        return $"Successfully added table '{physicalName}'.";
    }

    [McpServerTool]
    [Description("Deletes a table definition from tables.csv.")]
    public string DeleteTable([Description("The physical name of the table to delete.")] string physicalName)
    {
        _logger.LogInformation("Executing DeleteTable tool for {PhysicalName}", physicalName);
        _editorService.DeleteRecords<Table>(t => t.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase), "tables.csv");
        return $"Successfully deleted table '{physicalName}'.";
    }

    [McpServerTool]
    [Description("Adds a new column definition to columns.csv.")]
    public string AddColumn(
        [Description("The physical name of the table this column belongs to.")] string tablePhysicalName,
        [Description("The logical name of the column.")] string logicalName,
        [Description("The physical name of the column.")] string physicalName,
        [Description("The data type of the column.")] string dataType,
        [Description("A description of the column.")] string? description = null)
    {
        _logger.LogInformation("Executing AddColumn tool for {TablePhysicalName}.{PhysicalName}", tablePhysicalName, physicalName);
        var column = new Column { TablePhysicalName = tablePhysicalName, LogicalName = logicalName, PhysicalName = physicalName, DataType = dataType, Description = description };
        _editorService.AddRecord(column, "columns.csv");
        return $"Successfully added column '{physicalName}' to table '{tablePhysicalName}'.";
    }

    [McpServerTool]
    [Description("Deletes a column definition from columns.csv.")]
    public string DeleteColumn(
        [Description("The physical name of the table the column belongs to.")] string tablePhysicalName,
        [Description("The physical name of the column to delete.")] string physicalName)
    {
        _logger.LogInformation("Executing DeleteColumn tool for {TablePhysicalName}.{PhysicalName}", tablePhysicalName, physicalName);
        _editorService.DeleteRecords<Column>(c =>
            c.TablePhysicalName.Equals(tablePhysicalName, StringComparison.OrdinalIgnoreCase) &&
            c.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase),
            "columns.csv");
        return $"Successfully deleted column '{physicalName}' from table '{tablePhysicalName}'.";
    }

    [McpServerTool]
    [Description("Adds a new relationship definition to relations.csv.")]
    public string AddRelation(
        [Description("The source table's physical name.")] string sourceTable,
        [Description("The source column's physical name.")] string sourceColumn,
        [Description("The target table's physical name.")] string targetTable,
        [Description("The target column's physical name.")] string targetColumn)
    {
        _logger.LogInformation("Executing AddRelation tool for {SourceTable} -> {TargetTable}", sourceTable, targetTable);
        var relation = new Relation { SourceTable = sourceTable, SourceColumn = sourceColumn, TargetTable = targetTable, TargetColumn = targetColumn };
        _editorService.AddRecord(relation, "relations.csv");
        return $"Successfully added relation between '{sourceTable}' and '{targetTable}'.";
    }

    [McpServerTool]
    [Description("Deletes a relationship definition from relations.csv.")]
    public string DeleteRelation(
        [Description("The source table's physical name.")] string sourceTable,
        [Description("The source column's physical name.")] string sourceColumn,
        [Description("The target table's physical name.")] string targetTable,
        [Description("The target column's physical name.")] string targetColumn)
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
