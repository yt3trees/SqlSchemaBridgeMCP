using Microsoft.Extensions.Logging;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services.Database;

namespace SqlSchemaBridgeMCP.Services;

public class DatabaseSchemaImportService
{
    private readonly DatabaseSchemaProviderFactory _providerFactory;
    private readonly CsvConverterService _csvConverter;
    private readonly ILogger<DatabaseSchemaImportService> _logger;

    public DatabaseSchemaImportService(
        DatabaseSchemaProviderFactory providerFactory,
        CsvConverterService csvConverter,
        ILogger<DatabaseSchemaImportService> logger)
    {
        _providerFactory = providerFactory;
        _csvConverter = csvConverter;
        _logger = logger;
    }

    public async Task<DatabaseImportResult> ImportSchemaAsync(DatabaseConnection connection, string? profileName = null)
    {
        var result = new DatabaseImportResult
        {
            ConnectionName = connection.Name,
            DatabaseType = connection.Type,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting schema import for connection: {ConnectionName} ({DatabaseType})",
                connection.Name, connection.Type);

            var provider = _providerFactory.CreateProvider(connection.Type);

            // Test connection first
            var canConnect = await provider.TestConnectionAsync(connection.ConnectionString);
            if (!canConnect)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to connect to database";
                return result;
            }

            // Import schema data
            var tables = await provider.GetTablesAsync(connection.ConnectionString);
            var columns = await provider.GetColumnsAsync(connection.ConnectionString);
            var relations = await provider.GetRelationsAsync(connection.ConnectionString);

            result.TablesCount = tables.Count;
            result.ColumnsCount = columns.Count;
            result.RelationsCount = relations.Count;

            // Save to CSV files if profile name is provided
            if (!string.IsNullOrEmpty(profileName))
            {
                var profilePath = GetProfilePath(profileName);
                Directory.CreateDirectory(profilePath);

                // Use AppendCsvAsync to add data to existing profile instead of overwriting
                await _csvConverter.AppendCsvAsync(tables, Path.Combine(profilePath, "tables.csv"));
                await _csvConverter.AppendCsvAsync(columns, Path.Combine(profilePath, "columns.csv"));
                await _csvConverter.AppendCsvAsync(relations, Path.Combine(profilePath, "relations.csv"));

                result.ProfilePath = profilePath;
                _logger.LogInformation("Schema data appended to profile: {ProfileName}", profileName);
            }

            result.Success = true;
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Schema import completed successfully. Tables: {Tables}, Columns: {Columns}, Relations: {Relations}",
                result.TablesCount, result.ColumnsCount, result.RelationsCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema import failed for connection: {ConnectionName}", connection.Name);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    public async Task<bool> TestConnectionAsync(DatabaseConnection connection)
    {
        try
        {
            var provider = _providerFactory.CreateProvider(connection.Type);
            return await provider.TestConnectionAsync(connection.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for: {ConnectionName}", connection.Name);
            return false;
        }
    }

    public IReadOnlyList<DatabaseType> GetSupportedDatabaseTypes()
    {
        return _providerFactory.GetSupportedDatabaseTypes();
    }

    private string GetProfilePath(string profileName)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profilesPath = Environment.GetEnvironmentVariable("SQLSCHEMABRIDGEMCP_PROFILES_PATH") ??
                          Path.Combine(basePath, ".SqlSchemaBridgeMCP");
        return Path.Combine(profilesPath, profileName);
    }
}

public class DatabaseImportResult
{
    public string ConnectionName { get; set; } = string.Empty;
    public DatabaseType DatabaseType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TablesCount { get; set; }
    public int ColumnsCount { get; set; }
    public int RelationsCount { get; set; }
    public string? ProfilePath { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public TimeSpan? Duration => EndTime?.Subtract(StartTime);
}