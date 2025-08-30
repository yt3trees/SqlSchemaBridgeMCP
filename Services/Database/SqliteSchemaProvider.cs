using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services.Database;

public class SqliteSchemaProvider : IDatabaseSchemaProvider
{
    private readonly ILogger<SqliteSchemaProvider> _logger;

    public SqliteSchemaProvider(ILogger<SqliteSchemaProvider> logger)
    {
        _logger = logger;
    }

    public string GetDisplayName() => "SQLite";

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SQLite database");
            return false;
        }
    }

    public async Task<IReadOnlyList<Table>> GetTablesAsync(string connectionString)
    {
        const string query = @"
            SELECT
                'main' as database_name,
                '' as schema_name,
                name as physical_name,
                name as logical_name,
                '' as primary_key,
                '' as description
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name";

        var tables = new List<Table>();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString("physical_name");
                var primaryKey = await GetPrimaryKeyAsync(connection, tableName);

                tables.Add(new Table
                {
                    DatabaseName = reader.GetString("database_name"),
                    SchemaName = reader.GetString("schema_name"),
                    PhysicalName = tableName,
                    LogicalName = reader.GetString("logical_name"),
                    PrimaryKey = primaryKey,
                    Description = reader.GetString("description")
                });
            }

            _logger.LogInformation("Retrieved {Count} tables from SQLite database", tables.Count);
            return tables.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tables from SQLite database");
            throw;
        }
    }

    private async Task<string> GetPrimaryKeyAsync(SqliteConnection connection, string tableName)
    {
        var query = $"PRAGMA table_info('{tableName}')";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var isPk = reader.GetInt32("pk");
            if (isPk == 1)
            {
                return reader.GetString("name");
            }
        }

        return string.Empty;
    }

    public async Task<IReadOnlyList<Column>> GetColumnsAsync(string connectionString)
    {
        var columns = new List<Column>();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // First get all table names
            var tableQuery = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            var tableNames = new List<string>();

            using (var tableCommand = new SqliteCommand(tableQuery, connection))
            using (var tableReader = await tableCommand.ExecuteReaderAsync())
            {
                while (await tableReader.ReadAsync())
                {
                    tableNames.Add(tableReader.GetString("name"));
                }
            }

            // Get column info for each table
            foreach (var tableName in tableNames)
            {
                var columnQuery = $"PRAGMA table_info('{tableName}')";

                using var command = new SqliteCommand(columnQuery, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    columns.Add(new Column
                    {
                        TablePhysicalName = tableName,
                        PhysicalName = reader.GetString("name"),
                        LogicalName = reader.GetString("name"),
                        DataType = reader.GetString("type"),
                        Description = string.Empty
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} columns from SQLite database", columns.Count);
            return columns.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve columns from SQLite database");
            throw;
        }
    }

    public async Task<IReadOnlyList<Relation>> GetRelationsAsync(string connectionString)
    {
        var relations = new List<Relation>();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // First get all table names
            var tableQuery = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            var tableNames = new List<string>();

            using (var tableCommand = new SqliteCommand(tableQuery, connection))
            using (var tableReader = await tableCommand.ExecuteReaderAsync())
            {
                while (await tableReader.ReadAsync())
                {
                    tableNames.Add(tableReader.GetString("name"));
                }
            }

            // Get foreign key info for each table
            foreach (var tableName in tableNames)
            {
                var fkQuery = $"PRAGMA foreign_key_list('{tableName}')";

                using var command = new SqliteCommand(fkQuery, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    relations.Add(new Relation
                    {
                        SourceTable = tableName,
                        SourceColumn = reader.GetString("from"),
                        TargetTable = reader.GetString("table"),
                        TargetColumn = reader.GetString("to")
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} relations from SQLite database", relations.Count);
            return relations.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relations from SQLite database");
            throw;
        }
    }
}