using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Repositories;

public class SchemaRepository : ISchemaRepository
{
    private readonly SchemaProvider _schemaProvider;

    public SchemaRepository(SchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public Task<IReadOnlyList<Table>> GetAllTablesAsync()
    {
        return Task.FromResult(_schemaProvider.Tables);
    }

    public Task<IReadOnlyList<Column>> GetAllColumnsAsync()
    {
        return Task.FromResult(_schemaProvider.Columns);
    }

    public Task<IReadOnlyList<Relation>> GetAllRelationsAsync()
    {
        return Task.FromResult(_schemaProvider.Relations);
    }
}