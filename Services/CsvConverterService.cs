using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Service for converting schema objects to CSV format.
/// </summary>
public class CsvConverterService
{
    private readonly ILogger<CsvConverterService> _logger;

    public CsvConverterService(ILogger<CsvConverterService> logger)
    {
        _logger = logger;
    }
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

    public async Task WriteCsvAsync<T>(IEnumerable<T> records, string filePath)
    {
        try
        {
            _logger.LogDebug("Writing CSV file to: {FilePath}", filePath);

            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            await csv.WriteRecordsAsync(records);
            _logger.LogInformation("Successfully wrote {Count} records to {FilePath}", records.Count(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write CSV file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Appends records to an existing CSV file. If the file doesn't exist, creates a new one with headers.
    /// </summary>
    public async Task AppendCsvAsync<T>(IEnumerable<T> records, string filePath)
    {
        try
        {
            var recordsList = records.ToList();
            if (recordsList.Count == 0)
            {
                _logger.LogDebug("No records to append to {FilePath}", filePath);
                return;
            }

            var fileExists = File.Exists(filePath);
            _logger.LogDebug("Appending {Count} records to CSV file: {FilePath} (file exists: {FileExists})", 
                recordsList.Count, filePath, fileExists);

            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write);
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // If the file doesn't exist or is empty, write headers
            if (!fileExists || new FileInfo(filePath).Length == 0)
            {
                csv.WriteHeader<T>();
                await csv.NextRecordAsync();
            }

            await csv.WriteRecordsAsync(recordsList);
            _logger.LogInformation("Successfully appended {Count} records to {FilePath}", recordsList.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append to CSV file: {FilePath}", filePath);
            throw;
        }
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