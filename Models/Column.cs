using CsvHelper.Configuration.Attributes;

namespace SqlSchemaBridgeMCP.Models;

/// <summary>
/// Represents a column definition from columns.csv.
/// </summary>
public record Column
{
    [Name("table_physical_name")]
    public required string TablePhysicalName { get; init; }

    [Name("logical_name")]
    public required string LogicalName { get; init; }

    [Name("physical_name")]
    public required string PhysicalName { get; init; }

    [Name("data_type")]
    public required string DataType { get; init; }

    [Name("description")]
    public string? Description { get; init; }
}
