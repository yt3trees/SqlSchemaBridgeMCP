using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Provides services for editing the schema CSV files.
/// </summary>
public class SchemaEditorService
{
    private readonly string _profilePath;
    private readonly SchemaProvider _schemaProvider;
    private readonly ILogger<SchemaEditorService> _logger;
    private readonly CsvConfiguration _csvConfig = new(CultureInfo.InvariantCulture);

    public SchemaEditorService(ProfileManager profileManager, SchemaProvider schemaProvider, ILogger<SchemaEditorService> logger)
    {
        _profilePath = profileManager.ProfilePath;
        _schemaProvider = schemaProvider;
        _logger = logger;
    }

    public void AddRecord<T>(T record, string fileName)
    {
        var filePath = Path.Combine(_profilePath, fileName);
        _logger.LogDebug("Adding record to {FilePath}", filePath);

        var records = ReadCsv<T>(filePath).ToList();
        records.Add(record);
        WriteCsv(records, filePath);

        _schemaProvider.Reload();
    }

    public void UpdateRecords<T>(Func<T, bool> predicate, Action<T> updateAction, string fileName)
    {
        var filePath = Path.Combine(_profilePath, fileName);
        _logger.LogDebug("Updating records in {FilePath}", filePath);

        var records = ReadCsv<T>(filePath).ToList();
        var recordsToUpdate = records.Where(predicate).ToList();

        if (recordsToUpdate.Count == 0)
        {
            _logger.LogWarning("No records found to update in {FilePath} for the given predicate.", filePath);
            return;
        }

        recordsToUpdate.ForEach(updateAction);
        WriteCsv(records, filePath);

        _schemaProvider.Reload();
    }

    public void DeleteRecords<T>(Func<T, bool> predicate, string fileName)
    {
        var filePath = Path.Combine(_profilePath, fileName);
        _logger.LogDebug("Deleting records from {FilePath}", filePath);

        var records = ReadCsv<T>(filePath).ToList();
        var recordsDeleted = records.RemoveAll(new Predicate<T>(predicate));

        if (recordsDeleted == 0)
        {
            _logger.LogWarning("No records found to delete in {FilePath} for the given predicate.", filePath);
            return;
        }

        WriteCsv(records, filePath);

        _schemaProvider.Reload();
    }

    private List<T> ReadCsv<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, _csvConfig);
            return csv.GetRecords<T>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CSV file for editing: {FilePath}", filePath);
            throw;
        }
    }

    private void WriteCsv<T>(IEnumerable<T> records, string filePath)
    {
        try
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, _csvConfig);
            csv.WriteRecords(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to CSV file: {FilePath}", filePath);
            throw;
        }
    }
}
