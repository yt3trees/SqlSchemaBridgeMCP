namespace SqlSchemaBridgeMCP.Models;

public class DatabaseConnection
{
    public string Name { get; set; } = string.Empty;
    public DatabaseType Type { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConnected { get; set; }
}

public enum DatabaseType
{
    SqlServer,
    MySQL,
    PostgreSQL,
    SQLite
}