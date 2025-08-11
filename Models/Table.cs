using CsvHelper.Configuration.Attributes;

namespace SqlSchemaBridgeMCP.Models;

/// <summary>
/// Represents a table definition from tables.csv.
/// </summary>
public record Table
{
    [Name("database_name")]
    public string? DatabaseName { get; init; }

    [Name("schema_name")]
    public string? SchemaName { get; init; }

    [Name("logical_name")]
    public required string LogicalName { get; init; }

    [Name("physical_name")]
    public required string PhysicalName { get; init; }

    [Name("primary_key")]
    public string? PrimaryKey { get; init; }

    [Name("description")]
    public string? Description { get; init; }
}
