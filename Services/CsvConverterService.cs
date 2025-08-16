using System.Text;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Service for converting schema objects to CSV format.
/// </summary>
public class CsvConverterService
{
    public string ConvertTablesToCsv(IEnumerable<Table> tables)
    {
        var csv = new StringBuilder();
        csv.AppendLine("database_name,schema_name,logical_name,physical_name,primary_key,description");

        foreach (var table in tables)
        {
            csv.AppendLine($"{EscapeCsvField(table.DatabaseName)},{EscapeCsvField(table.SchemaName)},{EscapeCsvField(table.LogicalName)},{EscapeCsvField(table.PhysicalName)},{EscapeCsvField(table.PrimaryKey)},{EscapeCsvField(table.Description)}");
        }

        return csv.ToString();
    }

    public string ConvertColumnsToCsv(IEnumerable<Column> columns)
    {
        var csv = new StringBuilder();
        csv.AppendLine("table_physical_name,logical_name,physical_name,data_type,description");

        foreach (var column in columns)
        {
            csv.AppendLine($"{EscapeCsvField(column.TablePhysicalName)},{EscapeCsvField(column.LogicalName)},{EscapeCsvField(column.PhysicalName)},{EscapeCsvField(column.DataType)},{EscapeCsvField(column.Description)}");
        }

        return csv.ToString();
    }

    public string ConvertRelationsToCsv(IEnumerable<Relation> relations)
    {
        var csv = new StringBuilder();
        csv.AppendLine("source_table,source_column,target_table,target_column");

        foreach (var relation in relations)
        {
            csv.AppendLine($"{EscapeCsvField(relation.SourceTable)},{EscapeCsvField(relation.SourceColumn)},{EscapeCsvField(relation.TargetTable)},{EscapeCsvField(relation.TargetColumn)}");
        }

        return csv.ToString();
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}