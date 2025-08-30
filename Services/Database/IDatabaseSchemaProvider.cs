using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services.Database;

public interface IDatabaseSchemaProvider
{
    Task<IReadOnlyList<Table>> GetTablesAsync(string connectionString);
    Task<IReadOnlyList<Column>> GetColumnsAsync(string connectionString);
    Task<IReadOnlyList<Relation>> GetRelationsAsync(string connectionString);
    Task<bool> TestConnectionAsync(string connectionString);
    string GetDisplayName();
}