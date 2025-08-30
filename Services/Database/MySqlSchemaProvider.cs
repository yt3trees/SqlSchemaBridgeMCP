using System.Data;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services.Database;

public class MySqlSchemaProvider : IDatabaseSchemaProvider
{
    private readonly ILogger<MySqlSchemaProvider> _logger;

    public MySqlSchemaProvider(ILogger<MySqlSchemaProvider> logger)
    {
        _logger = logger;
    }

    public string GetDisplayName() => "MySQL";

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MySQL database");
            return false;
        }
    }

    public async Task<IReadOnlyList<Table>> GetTablesAsync(string connectionString)
    {
        const string query = @"
            SELECT 
                t.TABLE_SCHEMA as database_name,
                '' as schema_name,
                t.TABLE_NAME as physical_name,
                t.TABLE_NAME as logical_name,
                COALESCE(
                    (SELECT c.COLUMN_NAME 
                     FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE c
                     WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA
                       AND c.TABLE_NAME = t.TABLE_NAME
                       AND c.CONSTRAINT_NAME = 'PRIMARY'
                     LIMIT 1), 
                    '') as primary_key,
                COALESCE(t.TABLE_COMMENT, '') as description
            FROM INFORMATION_SCHEMA.TABLES t
            WHERE t.TABLE_TYPE = 'BASE TABLE'
              AND t.TABLE_SCHEMA NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

        var tables = new List<Table>();

        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(new Table
                {
                    DatabaseName = reader.GetString("database_name"),
                    SchemaName = reader.GetString("schema_name"),
                    PhysicalName = reader.GetString("physical_name"),
                    LogicalName = reader.GetString("logical_name"),
                    PrimaryKey = reader.GetString("primary_key"),
                    Description = reader.GetString("description")
                });
            }

            _logger.LogInformation("Retrieved {Count} tables from MySQL database", tables.Count);
            return tables.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tables from MySQL database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Column>> GetColumnsAsync(string connectionString)
    {
        const string query = @"
            SELECT 
                c.TABLE_NAME as table_physical_name,
                c.COLUMN_NAME as physical_name,
                c.COLUMN_NAME as logical_name,
                c.COLUMN_TYPE as data_type,
                COALESCE(c.COLUMN_COMMENT, '') as description
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_SCHEMA NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
            ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";

        var columns = new List<Column>();

        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(new Column
                {
                    TablePhysicalName = reader.GetString("table_physical_name"),
                    PhysicalName = reader.GetString("physical_name"),
                    LogicalName = reader.GetString("logical_name"),
                    DataType = reader.GetString("data_type"),
                    Description = reader.GetString("description")
                });
            }

            _logger.LogInformation("Retrieved {Count} columns from MySQL database", columns.Count);
            return columns.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve columns from MySQL database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Relation>> GetRelationsAsync(string connectionString)
    {
        const string query = @"
            SELECT 
                kcu.TABLE_NAME as source_table,
                kcu.COLUMN_NAME as source_column,
                kcu.REFERENCED_TABLE_NAME as target_table,
                kcu.REFERENCED_COLUMN_NAME as target_column
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
            WHERE kcu.REFERENCED_TABLE_NAME IS NOT NULL
              AND kcu.TABLE_SCHEMA NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')
            ORDER BY kcu.TABLE_NAME, kcu.COLUMN_NAME, kcu.REFERENCED_TABLE_NAME, kcu.REFERENCED_COLUMN_NAME";

        var relations = new List<Relation>();

        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                relations.Add(new Relation
                {
                    SourceTable = reader.GetString("source_table"),
                    SourceColumn = reader.GetString("source_column"),
                    TargetTable = reader.GetString("target_table"),
                    TargetColumn = reader.GetString("target_column")
                });
            }

            _logger.LogInformation("Retrieved {Count} relations from MySQL database", relations.Count);
            return relations.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relations from MySQL database");
            throw;
        }
    }
}