using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

/// <summary>
/// Provides tools for querying database schema information.
/// </summary>
public class SqlSchemaBridgeTools
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
    public string SqlSchemaGetProfileInstructions()
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
    public string SqlSchemaFindTable(
        [Description("The logical name of the table (e.g., 'Customers')")] string? logicalName = null,
        [Description("The physical name of the table (e.g., 'M_CUSTOMERS')")] string? physicalName = null,
        [Description("The physical name of the database to search within.")] string? databaseName = null,
        [Description("The physical name of the schema to search within.")] string? schemaName = null,
        [Description("Specifies whether to perform an exact match (case-insensitive). Defaults to false (contains).")] bool exactMatch = false,
        [Description("Enables regular expression matching for name searches. Takes precedence over exactMatch if true.")] bool useRegex = false)
    {
        if (string.IsNullOrWhiteSpace(logicalName) && string.IsNullOrWhiteSpace(physicalName) && string.IsNullOrWhiteSpace(databaseName) && string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("At least one of the following must be provided: logical_name, physical_name, database_name, or schema_name.");
        }

        _logger.LogDebug("Searching for table with logicalName: '{LogicalName}', physicalName: '{PhysicalName}', databaseName: '{DatabaseName}', schemaName: '{SchemaName}', exactMatch: {ExactMatch}, useRegex: {UseRegex}", logicalName, physicalName, databaseName, schemaName, exactMatch, useRegex);

        var query = _schemaProvider.Tables.AsQueryable();

        if (!string.IsNullOrWhiteSpace(logicalName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(logicalName, RegexOptions.IgnoreCase);
                    query = query.Where(t => regex.IsMatch(t.LogicalName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for logicalName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(t => t.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(t => t.LogicalName.Contains(logicalName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(physicalName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(physicalName, RegexOptions.IgnoreCase);
                    query = query.Where(t => regex.IsMatch(t.PhysicalName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for physicalName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(t => t.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(t => t.PhysicalName.Contains(physicalName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(databaseName, RegexOptions.IgnoreCase);
                    query = query.Where(t => t.DatabaseName != null && regex.IsMatch(t.DatabaseName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for databaseName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(t => t.DatabaseName != null && t.DatabaseName.Equals(databaseName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(t => t.DatabaseName != null && t.DatabaseName.Contains(databaseName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(schemaName, RegexOptions.IgnoreCase);
                    query = query.Where(t => t.SchemaName != null && regex.IsMatch(t.SchemaName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for schemaName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(t => t.SchemaName != null && t.SchemaName.Equals(schemaName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(t => t.SchemaName != null && t.SchemaName.Contains(schemaName, StringComparison.OrdinalIgnoreCase));
            }
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
    [Description("Searches for columns by logical or physical name and returns results in CSV format. Can be filtered by table_name.")]
    public string SqlSchemaFindColumn(
        [Description("The logical name of the column (e.g., 'Customer Name')")] string? logicalName = null,
        [Description("The physical name of the column (e.g., 'CUSTOMER_NAME')")] string? physicalName = null,
        [Description("The physical name of the table to search within (e.g., 'M_CUSTOMERS')")] string? tableName = null,
        [Description("Specifies whether to perform an exact match (case-insensitive). Defaults to false (contains).")] bool exactMatch = false,
        [Description("Enables regular expression matching for name searches. Takes precedence over exactMatch if true.")] bool useRegex = false)
    {
        if (string.IsNullOrWhiteSpace(logicalName) && string.IsNullOrWhiteSpace(physicalName) && string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("At least one of the following must be provided: logical_name, physical_name, or table_name.");
        }

        _logger.LogDebug("Searching for column with logicalName: '{LogicalName}', physicalName: '{PhysicalName}', in table: '{TableName}', exactMatch: {ExactMatch}, useRegex: {UseRegex}", logicalName, physicalName, tableName, exactMatch, useRegex);

        var query = _schemaProvider.Columns.AsQueryable();

        if (!string.IsNullOrWhiteSpace(logicalName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(logicalName, RegexOptions.IgnoreCase);
                    query = query.Where(c => regex.IsMatch(c.LogicalName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for logicalName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(c => c.LogicalName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(c => c.LogicalName.Contains(logicalName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(physicalName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(physicalName, RegexOptions.IgnoreCase);
                    query = query.Where(c => regex.IsMatch(c.PhysicalName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for physicalName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(c => c.PhysicalName.Equals(physicalName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(c => c.PhysicalName.Contains(physicalName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(tableName, RegexOptions.IgnoreCase);
                    query = query.Where(c => regex.IsMatch(c.TablePhysicalName));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression for tableName: {ex.Message}", ex);
                }
            }
            else
            {
                query = exactMatch
                    ? query.Where(c => c.TablePhysicalName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                    : query.Where(c => c.TablePhysicalName.Contains(tableName, StringComparison.OrdinalIgnoreCase));
            }
        }

        var results = query.ToList();

        if (!results.Any())
        {
            _logger.LogInformation("Column not found for the given criteria.");
            return "table_physical_name,logical_name,physical_name,data_type,description";
        }

        const int maxResultCount = 500;
        if (results.Count > maxResultCount)
        {
            var totalCount = results.Count;
            _logger.LogWarning("Too many results found: {Count}. Returning first {MaxCount} results", results.Count, maxResultCount);
            results = results.Take(maxResultCount).ToList();

            var csvData = _csvConverter.ConvertColumnsToCsv(results);
            var errorMessage = $"WARNING: Too many results found ({totalCount}). Showing first {maxResultCount} results only. Try using exactMatch=true for more precise results.";
            return $"{errorMessage}\n\n{csvData}";
        }

        return _csvConverter.ConvertColumnsToCsv(results);
    }

    [McpServerTool]
    [Description("Finds relationships and join conditions for a specified table and returns results in CSV format.")]
    public string SqlSchemaFindRelations(
        [Description("The physical name of the table (e.g., 'M_CUSTOMERS')")] string tableName,
        [Description("Specifies whether to perform an exact match (case-insensitive). Defaults to false (contains).")] bool exactMatch = false,
        [Description("Enables regular expression matching for table name search. Takes precedence over exactMatch if true.")] bool useRegex = false)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("The table_name must be provided.");
        }

        _logger.LogDebug("Searching for relations involving table: '{TableName}', exactMatch: {ExactMatch}, useRegex: {UseRegex}", tableName, exactMatch, useRegex);

        var query = _schemaProvider.Relations.AsQueryable();

        if (useRegex)
        {
            try
            {
                var regex = new Regex(tableName, RegexOptions.IgnoreCase);
                query = query.Where(r => regex.IsMatch(r.SourceTable) || regex.IsMatch(r.TargetTable));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regular expression for tableName: {ex.Message}", ex);
            }
        }
        else
        {
            query = exactMatch
                ? query.Where(r => r.SourceTable.Equals(tableName, StringComparison.OrdinalIgnoreCase) || r.TargetTable.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                : query.Where(r => r.SourceTable.Contains(tableName, StringComparison.OrdinalIgnoreCase) || r.TargetTable.Contains(tableName, StringComparison.OrdinalIgnoreCase));
        }

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
    public string SqlSchemaListTables()
    {
        _logger.LogInformation("Returning all tables.");
        return _csvConverter.ConvertTablesToCsv(_schemaProvider.Tables);
    }
}
