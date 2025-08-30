using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services.Database;

public class DatabaseSchemaProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public DatabaseSchemaProviderFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IDatabaseSchemaProvider CreateProvider(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerSchemaProvider(_loggerFactory.CreateLogger<SqlServerSchemaProvider>()),
            DatabaseType.MySQL => new MySqlSchemaProvider(_loggerFactory.CreateLogger<MySqlSchemaProvider>()),
            DatabaseType.PostgreSQL => new PostgreSqlSchemaProvider(_loggerFactory.CreateLogger<PostgreSqlSchemaProvider>()),
            DatabaseType.SQLite => new SqliteSchemaProvider(_loggerFactory.CreateLogger<SqliteSchemaProvider>()),
            _ => throw new NotSupportedException($"Database type {databaseType} is not supported")
        };
    }

    public IReadOnlyList<DatabaseType> GetSupportedDatabaseTypes()
    {
        return new[] { DatabaseType.SqlServer, DatabaseType.MySQL, DatabaseType.PostgreSQL, DatabaseType.SQLite };
    }
}