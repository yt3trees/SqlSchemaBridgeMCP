using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Repositories;

public interface ISchemaRepository
{
    Task<IReadOnlyList<Table>> GetAllTablesAsync();
    Task<IReadOnlyList<Column>> GetAllColumnsAsync();
    Task<IReadOnlyList<Relation>> GetAllRelationsAsync();
}