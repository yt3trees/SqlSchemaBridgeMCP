using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services.Database;

public class SqlServerSchemaProvider : IDatabaseSchemaProvider
{
    private readonly ILogger<SqlServerSchemaProvider> _logger;

    public SqlServerSchemaProvider(ILogger<SqlServerSchemaProvider> logger)
    {
        _logger = logger;
    }

    public string GetDisplayName() => "SQL Server";

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SQL Server database");
            return false;
        }
    }

    public async Task<IReadOnlyList<Table>> GetTablesAsync(string connectionString)
    {
        const string query = @"
            SELECT 
                DB_NAME() as database_name,
                SCHEMA_NAME(t.schema_id) as schema_name,
                t.name as physical_name,
                t.name as logical_name,
                COALESCE(
                    (SELECT c.name
                     FROM sys.key_constraints kc
                     JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id
                                                AND kc.unique_index_id = ic.index_id
                     JOIN sys.columns c ON ic.object_id = c.object_id
                                        AND ic.column_id = c.column_id
                     WHERE kc.parent_object_id = t.object_id
                       AND kc.type = 'PK'
                       AND ic.index_column_id = 1),
                    '') as primary_key,
                COALESCE(ep.value, '') as description
            FROM sys.tables t
            LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id
                                                  AND ep.minor_id = 0
                                                  AND ep.name = 'MS_Description'
            ORDER BY SCHEMA_NAME(t.schema_id), t.name";

        var tables = new List<Table>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
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

            _logger.LogInformation("Retrieved {Count} tables from SQL Server database", tables.Count);
            return tables.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tables from SQL Server database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Column>> GetColumnsAsync(string connectionString)
    {
        const string query = @"
            SELECT
                t.name as table_physical_name,
                c.name as physical_name,
                c.name as logical_name,
                CASE
                    WHEN c.max_length = -1 THEN ty.name + '(max)'
                    WHEN ty.name IN ('nchar', 'nvarchar') THEN ty.name + '(' + CAST(c.max_length/2 AS VARCHAR(10)) + ')'
                    WHEN ty.name IN ('char', 'varchar', 'binary', 'varbinary') THEN ty.name + '(' + CAST(c.max_length AS VARCHAR(10)) + ')'
                    WHEN ty.name IN ('decimal', 'numeric') THEN ty.name + '(' + CAST(c.precision AS VARCHAR(10)) + ',' + CAST(c.scale AS VARCHAR(10)) + ')'
                    WHEN ty.name IN ('float') THEN ty.name + '(' + CAST(c.precision AS VARCHAR(10)) + ')'
                    ELSE ty.name
                END as data_type,
                COALESCE(ep.value, '') as description
            FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            LEFT JOIN sys.extended_properties ep ON ep.major_id = c.object_id
                                                  AND ep.minor_id = c.column_id
                                                  AND ep.name = 'MS_Description'
            ORDER BY t.name, c.column_id";

        var columns = new List<Column>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
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

            _logger.LogInformation("Retrieved {Count} columns from SQL Server database", columns.Count);
            return columns.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve columns from SQL Server database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Relation>> GetRelationsAsync(string connectionString)
    {
        const string query = @"
            SELECT 
                pt.name as source_table,
                pc.name as source_column,
                rt.name as target_table,
                rc.name as target_column
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
            JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
            JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
            JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
            ORDER BY pt.name, pc.name, rt.name, rc.name";

        var relations = new List<Relation>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
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

            _logger.LogInformation("Retrieved {Count} relations from SQL Server database", relations.Count);
            return relations.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relations from SQL Server database");
            throw;
        }
    }
}