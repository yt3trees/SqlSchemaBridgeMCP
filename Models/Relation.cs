using CsvHelper.Configuration.Attributes;

namespace SqlSchemaBridgeMCP.Models;

/// <summary>
/// Represents a table relation from relations.csv.
/// </summary>
public record Relation
{
    [Name("source_table")]
    public required string SourceTable { get; init; }

    [Name("source_column")]
    public required string SourceColumn { get; init; }

    [Name("target_table")]
    public required string TargetTable { get; init; }

    [Name("target_column")]
    public required string TargetColumn { get; init; }
}
