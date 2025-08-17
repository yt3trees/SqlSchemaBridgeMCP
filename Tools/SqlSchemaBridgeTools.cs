using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

/// <summary>
/// Provides tools for querying database schema information.
/// </summary>
internal class SqlSchemaBridgeTools
{
    private readonly ProfileManager _profileManager;
    private readonly SchemaProvider _schemaProvider;
    private readonly CsvConverterService _csvConverter;
    private readonly ILogger<SqlSchemaBridgeTools> _logger;

    public SqlSchemaBridgeTools(ProfileManager profileManager, SchemaProvider schemaProvider, CsvConverterService csvConverter, ILogger<SqlSchemaBridgeTools> logger)
    {
        _profileManager = profileManager;
        _schemaProvider = schemaProvider;
        _csvConverter = csvConverter;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Gets the instructions for the AI, if a README.md file is present in the current profile's directory. This tool must be executed first when using this MCP server.")]
    public string GetProfileInstructions()
    {
        if (!string.IsNullOrWhiteSpace(_profileManager.CurrentProfileReadme))
        {
            _logger.LogInformation("Returning profile instructions from README.md.");
            return _profileManager.CurrentProfileReadme;
        }

        _logger.LogInformation("No profile instructions (README.md) found for the current profile.");
        return "No specific instructions were provided for this profile.";
    }

    [McpServerTool]
    [Description("Searches for tables by logical or physical name and returns all matches in CSV format.")]
    public string FindTable(
        [Description("The logical name of the table (e.g., 'Customers')")] string? logicalName = null,
        [Description("The physical name of the table (e.g., 'M_CUSTOMERS')")] string? physicalName = null,
        [Description("The physical name of the database to search within.")] string? databaseName = null,
        [Description("The physical name of the schema to search within.")] string? schemaName = null,
        [Description("Specifies whether to perform an exact match (case-insensitive). Defaults to false (contains).")] bool exactMatch = false)
    {
        if (string.IsNullOrWhiteSpace(logicalName) && string.IsNullOrWhiteSpace(physicalName) && string.IsNullOrWhiteSpace(databaseName) && string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("At least one of the following must be provided: logical_name, physical_name, database_name, or schema_name.");
        }

        _logger.LogDebug("Searching for table with logicalName: '{LogicalName}', physicalName: '{PhysicalName}', databaseName: '{DatabaseName}', schemaName: '{SchemaName}', exactMatch: {ExactMatch}", logicalName, physicalName, databaseName, schemaName, exactMatch);

        var query = _schemaProvider.Tables.AsQueryable();

        if (!string.IsNullOrWhiteSpace(logicalName))
        {
            query = exactMatch
                ? query.Where(t => t.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                : query.Where(t => t.LogicalName.Contains(logicalName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(physicalName))
        {
            query = exactMatch
                ? query.Where(t => t.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase))
                : query.Where(t => t.PhysicalName.Contains(physicalName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            query = exactMatch
                ? query.Where(t => t.DatabaseName != null && t.DatabaseName.Equals(databaseName, StringComparison.OrdinalIgnoreCase))
                : query.Where(t => t.DatabaseName != null && t.DatabaseName.Contains(databaseName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            query = exactMatch
                ? query.Where(t => t.SchemaName != null && t.SchemaName.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
                : query.Where(t => t.SchemaName != null && t.SchemaName.Contains(schemaName, StringComparison.OrdinalIgnoreCase));
        }

        var results = query.ToList();

        if (!results.Any())
        {
            _logger.LogInformation("Table not found for the given criteria.");
            return "database_name,schema_name,logical_name,physical_name,primary_key,description";
        }

        return _csvConverter.ConvertTablesToCsv(results);
    }

    [McpServerTool]
    [Description("Searches for columns by logical or physical name and returns results in CSV format. The search can be filtered by providing a table_name. If only a table_name is provided, all columns for that table are returned. Note: If the result is too large and causes token limit issues, try using exactMatch=true to get more specific results.")]
    public string FindColumn(
        [Description("The logical name of the column (e.g., 'Customer Name')")] string? logicalName = null,
        [Description("The physical name of the column (e.g., 'CUSTOMER_NAME')")] string? physicalName = null,
        [Description("The physical name of the table to search within (e.g., 'M_CUSTOMERS')")] string? tableName = null,
        [Description("Specifies whether to perform an exact match (case-insensitive). Defaults to false (contains).")] bool exactMatch = false)
    {
        if (string.IsNullOrWhiteSpace(logicalName) && string.IsNullOrWhiteSpace(physicalName) && string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("At least one of the following must be provided: logical_name, physical_name, or table_name.");
        }

        _logger.LogDebug("Searching for column with logicalName: '{LogicalName}', physicalName: '{PhysicalName}', in table: '{TableName}', exactMatch: {ExactMatch}", logicalName, physicalName, tableName, exactMatch);

        var query = _schemaProvider.Columns.AsQueryable();

        if (!string.IsNullOrWhiteSpace(logicalName))
        {
            query = exactMatch
                ? query.Where(c => c.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                : query.Where(c => c.LogicalName.Contains(logicalName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(physicalName))
        {
            query = exactMatch
                ? query.Where(c => c.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase))
                : query.Where(c => c.PhysicalName.Contains(physicalName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = exactMatch
                ? query.Where(c => c.TablePhysicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                : query.Where(c => c.TablePhysicalName.Contains(tableName, StringComparison.OrdinalIgnoreCase));
        }

        var results = query.ToList();

        if (!results.Any())
        {
            _logger.LogInformation("Column not found for the given criteria.");
            return "table_physical_name,logical_name,physical_name,data_type,description";
        }

        const int maxResultCount = 1000;
        if (results.Count > maxResultCount)
        {
            _logger.LogWarning("Too many results found: {Count}. Maximum allowed: {MaxCount}", results.Count, maxResultCount);
            throw new InvalidOperationException($"Too many results found ({results.Count} items). Maximum allowed is {maxResultCount} items. Please specify a table name to narrow down the search or set exactMatch=true.");
        }

        return _csvConverter.ConvertColumnsToCsv(results);
    }

    [McpServerTool]
    [Description("Finds relationships and join conditions for a specified table and returns results in CSV format.")]
    public string FindRelations(
        [Description("The physical name of the table (e.g., 'M_CUSTOMERS')")] string tableName,
        [Description("Specifies whether to perform an exact match (case-insensitive). Defaults to false (contains).")] bool exactMatch = false)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("The table_name must be provided.");
        }

        _logger.LogDebug("Searching for relations involving table: '{TableName}', exactMatch: {ExactMatch}", tableName, exactMatch);

        var query = _schemaProvider.Relations.AsQueryable();

        query = exactMatch
            ? query.Where(r => r.SourceTable.Equals(tableName, StringComparison.OrdinalIgnoreCase) || r.TargetTable.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            : query.Where(r => r.SourceTable.Contains(tableName, StringComparison.OrdinalIgnoreCase) || r.TargetTable.Contains(tableName, StringComparison.OrdinalIgnoreCase));

        var results = query.ToList();

        if (!results.Any())
        {
            _logger.LogInformation("No relations found for table: '{TableName}'", tableName);
            return "source_table,source_column,target_table,target_column";
        }

        return _csvConverter.ConvertRelationsToCsv(results);
    }

    [McpServerTool]
    [Description("Lists all available tables in CSV format.")]
    public string ListTables()
    {
        _logger.LogInformation("Returning all tables.");
        return _csvConverter.ConvertTablesToCsv(_schemaProvider.Tables);
    }
}
