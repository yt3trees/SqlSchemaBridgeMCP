using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Models;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

[Description("Tools for connecting to databases and importing schema information")]
public class DatabaseConnectionTools
{
    private readonly DatabaseSchemaImportService _importService;
    private readonly ProfileManager _profileManager;
    private readonly SchemaProvider _schemaProvider;
    private readonly ILogger<DatabaseConnectionTools> _logger;

    public DatabaseConnectionTools(
        DatabaseSchemaImportService importService,
        ProfileManager profileManager,
        SchemaProvider schemaProvider,
        ILogger<DatabaseConnectionTools> logger)
    {
        _importService = importService;
        _profileManager = profileManager;
        _schemaProvider = schemaProvider;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Test connection to a database")]
    public async Task<string> TestDatabaseConnection(
        [Description("Database type (SqlServer, MySQL, PostgreSQL, SQLite)")] string databaseType,
        [Description("Connection string for the database")] string connectionString)
    {
        try
        {
            if (!Enum.TryParse<DatabaseType>(databaseType, ignoreCase: true, out var dbType))
            {
                var result = new
                {
                    success = false,
                    error = $"Unsupported database type: {databaseType}. Supported types: {string.Join(", ", _importService.GetSupportedDatabaseTypes())}"
                };
                return JsonSerializer.Serialize(result);
            }

            var connection = new DatabaseConnection
            {
                Name = "test-connection",
                Type = dbType,
                ConnectionString = connectionString
            };

            var canConnect = await _importService.TestConnectionAsync(connection);

            var response = new
            {
                success = canConnect,
                database_type = databaseType,
                message = canConnect ? "Connection successful" : "Connection failed"
            };
            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing database connection");
            var errorResult = new { success = false, error = ex.Message };
            return JsonSerializer.Serialize(errorResult);
        }
    }

    [McpServerTool]
    [Description("Import database schema from a live database and save to a profile")]
    public async Task<string> ImportDatabaseSchema(
        [Description("Database type (SqlServer, MySQL, PostgreSQL, SQLite)")] string databaseType,
        [Description("Connection string for the database")] string connectionString,
        [Description("Profile name to save the imported schema to")] string profileName,
        [Description("Optional connection name for reference")] string? connectionName = null)
    {
        try
        {
            if (!Enum.TryParse<DatabaseType>(databaseType, ignoreCase: true, out var dbType))
            {
                var errorResult = new
                {
                    success = false,
                    error = $"Unsupported database type: {databaseType}. Supported types: {string.Join(", ", _importService.GetSupportedDatabaseTypes())}"
                };
                return JsonSerializer.Serialize(errorResult);
            }

            var connection = new DatabaseConnection
            {
                Name = connectionName ?? $"{databaseType}-connection",
                Type = dbType,
                ConnectionString = connectionString
            };

            var importResult = await _importService.ImportSchemaAsync(connection, profileName);

            if (importResult.Success)
            {
                var response = new
                {
                    success = true,
                    profile_name = profileName,
                    database_type = importResult.DatabaseType.ToString(),
                    tables_count = importResult.TablesCount,
                    columns_count = importResult.ColumnsCount,
                    relations_count = importResult.RelationsCount,
                    profile_path = importResult.ProfilePath,
                    duration_ms = importResult.Duration?.TotalMilliseconds ?? 0,
                    message = $"Successfully imported schema to profile '{profileName}'"
                };
                return JsonSerializer.Serialize(response);
            }
            else
            {
                var errorResponse = new
                {
                    success = false,
                    error = importResult.ErrorMessage
                };
                return JsonSerializer.Serialize(errorResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing database schema");
            var errorResult = new { success = false, error = ex.Message };
            return JsonSerializer.Serialize(errorResult);
        }
    }

    [McpServerTool]
    [Description("Import database schema and switch to the new profile immediately")]
    public async Task<string> ImportAndSwitchToProfile(
        [Description("Database type (SqlServer, MySQL, PostgreSQL, SQLite)")] string databaseType,
        [Description("Connection string for the database")] string connectionString,
        [Description("Profile name to create and switch to")] string profileName,
        [Description("Optional connection name for reference")] string? connectionName = null)
    {
        try
        {
            // First import the schema
            var importResultJson = await ImportDatabaseSchema(databaseType, connectionString, profileName, connectionName);
            var importResult = JsonSerializer.Deserialize<JsonElement>(importResultJson);

            if (importResult.GetProperty("success").GetBoolean())
            {
                // Switch to the new profile
                await _profileManager.SwitchProfileAsync(profileName);
                _schemaProvider.Reload();

                var currentProfile = new
                {
                    name = _profileManager.CurrentProfile,
                    path = _profileManager.CurrentProfilePath
                };

                var response = new
                {
                    success = true,
                    profile_name = profileName,
                    database_type = databaseType,
                    tables_count = _schemaProvider.Tables.Count,
                    columns_count = _schemaProvider.Columns.Count,
                    relations_count = _schemaProvider.Relations.Count,
                    current_profile = currentProfile,
                    message = $"Successfully imported schema and switched to profile '{profileName}'"
                };
                return JsonSerializer.Serialize(response);
            }

            return importResultJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing and switching to profile");
            var errorResult = new { success = false, error = ex.Message };
            return JsonSerializer.Serialize(errorResult);
        }
    }

    [McpServerTool]
    [Description("Get information about supported database types")]
    public string GetSupportedDatabaseTypes()
    {
        try
        {
            var supportedTypes = _importService.GetSupportedDatabaseTypes();

            var response = new
            {
                supported_types = supportedTypes.Select(t => new
                {
                    type = t.ToString(),
                    display_name = t switch
                    {
                        DatabaseType.SqlServer => "Microsoft SQL Server",
                        DatabaseType.MySQL => "MySQL",
                        DatabaseType.PostgreSQL => "PostgreSQL",
                        DatabaseType.SQLite => "SQLite",
                        _ => t.ToString()
                    }
                }).ToArray(),
                count = supportedTypes.Count
            };
            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supported database types");
            var errorResult = new { success = false, error = ex.Message };
            return JsonSerializer.Serialize(errorResult);
        }
    }
}