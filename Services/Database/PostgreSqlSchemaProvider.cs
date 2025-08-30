using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services.Database;

public class PostgreSqlSchemaProvider : IDatabaseSchemaProvider
{
    private readonly ILogger<PostgreSqlSchemaProvider> _logger;

    public PostgreSqlSchemaProvider(ILogger<PostgreSqlSchemaProvider> logger)
    {
        _logger = logger;
    }

    public string GetDisplayName() => "PostgreSQL";

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to PostgreSQL database");
            return false;
        }
    }

    public async Task<IReadOnlyList<Table>> GetTablesAsync(string connectionString)
    {
        const string query = @"
            SELECT
                current_database() as database_name,
                t.table_schema as schema_name,
                t.table_name as physical_name,
                t.table_name as logical_name,
                COALESCE(
                    (SELECT c.column_name
                     FROM information_schema.key_column_usage c
                     JOIN information_schema.table_constraints tc ON c.constraint_name = tc.constraint_name
                     WHERE tc.table_name = t.table_name
                       AND tc.table_schema = t.table_schema
                       AND tc.constraint_type = 'PRIMARY KEY'
                     LIMIT 1),
                    '') as primary_key,
                COALESCE(
                    (SELECT obj_description(pg_class.oid)
                     FROM pg_class
                     JOIN pg_namespace ON pg_class.relnamespace = pg_namespace.oid
                     WHERE pg_class.relname = t.table_name
                       AND pg_namespace.nspname = t.table_schema),
                    '') as description
            FROM information_schema.tables t
            WHERE t.table_type = 'BASE TABLE'
              AND t.table_schema NOT IN ('information_schema', 'pg_catalog')
            ORDER BY t.table_schema, t.table_name";

        var tables = new List<Table>();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(query, connection);
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

            _logger.LogInformation("Retrieved {Count} tables from PostgreSQL database", tables.Count);
            return tables.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tables from PostgreSQL database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Column>> GetColumnsAsync(string connectionString)
    {
        const string query = @"
            SELECT
                c.table_name as table_physical_name,
                c.column_name as physical_name,
                c.column_name as logical_name,
                CASE
                    WHEN c.character_maximum_length IS NOT NULL THEN
                        c.data_type || '(' || c.character_maximum_length::text || ')'
                    WHEN c.numeric_precision IS NOT NULL AND c.numeric_scale IS NOT NULL THEN
                        c.data_type || '(' || c.numeric_precision::text || ',' || c.numeric_scale::text || ')'
                    WHEN c.numeric_precision IS NOT NULL THEN
                        c.data_type || '(' || c.numeric_precision::text || ')'
                    ELSE c.data_type
                END as data_type,
                COALESCE(
                    (SELECT col_description(pg_class.oid, c.ordinal_position)
                     FROM pg_class
                     JOIN pg_namespace ON pg_class.relnamespace = pg_namespace.oid
                     WHERE pg_class.relname = c.table_name
                       AND pg_namespace.nspname = c.table_schema),
                    '') as description
            FROM information_schema.columns c
            WHERE c.table_schema NOT IN ('information_schema', 'pg_catalog')
            ORDER BY c.table_name, c.ordinal_position";

        var columns = new List<Column>();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(query, connection);
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

            _logger.LogInformation("Retrieved {Count} columns from PostgreSQL database", columns.Count);
            return columns.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve columns from PostgreSQL database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Relation>> GetRelationsAsync(string connectionString)
    {
        const string query = @"
            SELECT
                kcu.table_name as source_table,
                kcu.column_name as source_column,
                ccu.table_name as target_table,
                ccu.column_name as target_column
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name
                AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
            ORDER BY kcu.table_name, kcu.column_name, ccu.table_name, ccu.column_name";

        var relations = new List<Relation>();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(query, connection);
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

            _logger.LogInformation("Retrieved {Count} relations from PostgreSQL database", relations.Count);
            return relations.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relations from PostgreSQL database");
            throw;
        }
    }
}