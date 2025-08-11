using System.Globalization;
using CsvHelper;
using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Loads and provides access to the database schema from CSV files.
/// The data is loaded once and cached in memory, with support for reloading.
/// </summary>
public class SchemaProvider
{
    private readonly string _profilePath;
    private readonly ILogger<SchemaProvider> _logger;

    public IReadOnlyList<Table> Tables { get; private set; } = [];
    public IReadOnlyList<Column> Columns { get; private set; } = [];
    public IReadOnlyList<Relation> Relations { get; private set; } = [];

    public SchemaProvider(ProfileManager profileManager, ILogger<SchemaProvider> logger)
    {
        _logger = logger;
        _profilePath = profileManager.ProfilePath;
        LoadData();
    }

    /// <summary>
    /// Reloads all schema information from the CSV files.
    /// </summary>
    public void Reload()
    {
        _logger.LogInformation("Reloading schema data...");
        LoadData();
    }

    private void LoadData()
    {
        Tables = LoadCsv<Table>(Path.Combine(_profilePath, "tables.csv"));
        Columns = LoadCsv<Column>(Path.Combine(_profilePath, "columns.csv"));
        Relations = LoadCsv<Relation>(Path.Combine(_profilePath, "relations.csv"));

        _logger.LogInformation("Loaded {TableCount} tables, {ColumnCount} columns, and {RelationCount} relations.", Tables.Count, Columns.Count, Relations.Count);
    }

    private IReadOnlyList<T> LoadCsv<T>(string filePath)
    {
        _logger.LogDebug("Loading CSV file from: {FilePath}", filePath);
        try
        {
            // If the file doesn't exist, return an empty list. This can happen if the user hasn't created all files yet.
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("CSV file not found, returning empty list: {FilePath}", filePath);
                return [];
            }

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<T>().ToList();
            return records.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load or parse CSV file: {FilePath}", filePath);
            // We return an empty list instead of throwing, so the server can start even with a malformed file.
            return [];
        }
    }
}
