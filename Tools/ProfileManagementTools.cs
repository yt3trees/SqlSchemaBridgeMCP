using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

public class ProfileManagementTools
{
    private readonly ProfileManager _profileManager;
    private readonly SchemaProvider _schemaProvider;
    private readonly CsvConverterService _csvConverter;

    public ProfileManagementTools(ProfileManager profileManager, SchemaProvider schemaProvider, CsvConverterService csvConverter)
    {
        _profileManager = profileManager;
        _schemaProvider = schemaProvider;
        _csvConverter = csvConverter;
    }

    [McpServerTool]
    [Description("Switches to a different profile and reloads schema data")]
    public async Task<object> SqlSchemaSwitchProfile(
        [Description("Name of the profile to switch to")]
        string profileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return new
                {
                    success = false,
                    error = "Profile name cannot be empty"
                };
            }

            var currentProfile = _profileManager.CurrentProfile;
            if (string.Equals(currentProfile, profileName, StringComparison.OrdinalIgnoreCase))
            {
                return new
                {
                    success = true,
                    message = $"Already using profile '{profileName}'",
                    current_profile = currentProfile,
                    profile_path = _profileManager.CurrentProfilePath
                };
            }

            var success = await _profileManager.SwitchProfileAsync(profileName);
            if (!success)
            {
                return new
                {
                    success = false,
                    error = $"Failed to switch to profile '{profileName}'. Profile directory may not exist or be inaccessible."
                };
            }

            // Reload schema data with the new profile
            _schemaProvider.Reload();

            return new
            {
                success = true,
                message = $"Successfully switched from '{currentProfile}' to '{profileName}'",
                previous_profile = currentProfile,
                current_profile = _profileManager.CurrentProfile,
                profile_path = _profileManager.CurrentProfilePath,
                schema_loaded = new
                {
                    tables = _schemaProvider.Tables.Count,
                    columns = _schemaProvider.Columns.Count,
                    relations = _schemaProvider.Relations.Count
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = $"Error occurred while switching profile: {ex.Message}"
            };
        }
    }

    [McpServerTool]
    [Description("Gets information about the current profile")]
    public Task<object> SqlSchemaGetCurrentProfile()
    {
        try
        {
            return Task.FromResult<object>(new
            {
                current_profile = _profileManager.CurrentProfile,
                profile_path = _profileManager.CurrentProfilePath,
                has_readme = !string.IsNullOrEmpty(_profileManager.CurrentProfileReadme),
                profile_exists = Directory.Exists(_profileManager.CurrentProfilePath),
                schema_loaded = new
                {
                    tables = _schemaProvider.Tables.Count,
                    columns = _schemaProvider.Columns.Count,
                    relations = _schemaProvider.Relations.Count
                }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                error = $"Error occurred while getting current profile information: {ex.Message}"
            });
        }
    }

    [McpServerTool]
    [Description("Reloads schema data from the current profile")]
    public Task<object> SqlSchemaReloadSchema()
    {
        try
        {
            var previousCounts = new
            {
                tables = _schemaProvider.Tables.Count,
                columns = _schemaProvider.Columns.Count,
                relations = _schemaProvider.Relations.Count
            };

            _schemaProvider.Reload();

            return Task.FromResult<object>(new
            {
                success = true,
                message = "Schema data reloaded successfully",
                current_profile = _profileManager.CurrentProfile,
                previous_counts = previousCounts,
                current_counts = new
                {
                    tables = _schemaProvider.Tables.Count,
                    columns = _schemaProvider.Columns.Count,
                    relations = _schemaProvider.Relations.Count
                }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = $"Error occurred while reloading schema: {ex.Message}"
            });
        }
    }

    [McpServerTool]
    [Description("Creates a new profile directory with optional initial schema files")]
    public async Task<object> SqlSchemaCreateProfile(
        [Description("Name of the profile to create")]
        string profileName,
        [Description("Optional description for the profile")]
        string? description = null,
        [Description("Whether to create sample CSV files (default: false)")]
        bool createSampleFiles = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return new
                {
                    success = false,
                    error = "Profile name cannot be empty"
                };
            }

            var profilePath = _profileManager.GetProfileDirectory(profileName);

            if (Directory.Exists(profilePath))
            {
                return new
                {
                    success = false,
                    error = $"Profile '{profileName}' already exists at: {profilePath}"
                };
            }

            Directory.CreateDirectory(profilePath);

            var createdFiles = new List<string>();

            if (!string.IsNullOrWhiteSpace(description))
            {
                var readmePath = Path.Combine(profilePath, "README.md");
                var readmeContent = $"# {profileName} Profile\n\n{description}\n\nCreated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";
                await File.WriteAllTextAsync(readmePath, readmeContent);
                createdFiles.Add("README.md");
            }

            if (createSampleFiles)
            {
                var tablesPath = Path.Combine(profilePath, "tables.csv");
                var columnsPath = Path.Combine(profilePath, "columns.csv");
                var relationsPath = Path.Combine(profilePath, "relations.csv");

                var sampleTablesContent = "database_name,schema_name,logical_name,physical_name,primary_key,description\nSampleDB,dbo,User Table,users,user_id,User information table\n";
                var sampleColumnsContent = "table_physical_name,logical_name,physical_name,data_type,description\nusers,User ID,user_id,int,Primary key for user\nusers,User Name,username,varchar(255),User's login name\n";
                var sampleRelationsContent = "source_table,source_column,target_table,target_column\norders,user_id,users,user_id\n";

                await File.WriteAllTextAsync(tablesPath, sampleTablesContent);
                await File.WriteAllTextAsync(columnsPath, sampleColumnsContent);
                await File.WriteAllTextAsync(relationsPath, sampleRelationsContent);

                createdFiles.AddRange(new[] { "tables.csv", "columns.csv", "relations.csv" });
            }

            return new
            {
                success = true,
                message = $"Profile '{profileName}' created successfully",
                profile_name = profileName,
                profile_path = profilePath,
                created_files = createdFiles.ToArray()
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = $"Error occurred while creating profile: {ex.Message}"
            };
        }
    }

    [McpServerTool]
    [Description("Generates CSV files for schema data based on specified types")]
    public async Task<object> SqlSchemaGenerateCSV(
        [Description("Type of CSV to generate: 'tables', 'columns', 'relations', or 'all'")]
        string csvType,
        [Description("Output directory path (optional, defaults to current profile directory)")]
        string? outputPath = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(csvType))
            {
                return new
                {
                    success = false,
                    error = "CSV type must be specified (tables, columns, relations, or all)"
                };
            }

            var normalizedType = csvType.ToLowerInvariant().Trim();
            var outputDirectory = outputPath ?? _profileManager.CurrentProfilePath;

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var results = new List<object>();

            switch (normalizedType)
            {
                case "tables":
                    var tablesPath = Path.Combine(outputDirectory, "tables.csv");
                    var tablesContent = _csvConverter.ConvertTablesToCsv(_schemaProvider.Tables);
                    await File.WriteAllTextAsync(tablesPath, tablesContent);
                    results.Add(new { type = "tables", path = tablesPath, record_count = _schemaProvider.Tables.Count });
                    break;

                case "columns":
                    var columnsPath = Path.Combine(outputDirectory, "columns.csv");
                    var columnsContent = _csvConverter.ConvertColumnsToCsv(_schemaProvider.Columns);
                    await File.WriteAllTextAsync(columnsPath, columnsContent);
                    results.Add(new { type = "columns", path = columnsPath, record_count = _schemaProvider.Columns.Count });
                    break;

                case "relations":
                    var relationsPath = Path.Combine(outputDirectory, "relations.csv");
                    var relationsContent = _csvConverter.ConvertRelationsToCsv(_schemaProvider.Relations);
                    await File.WriteAllTextAsync(relationsPath, relationsContent);
                    results.Add(new { type = "relations", path = relationsPath, record_count = _schemaProvider.Relations.Count });
                    break;

                case "all":
                    var allTablesPath = Path.Combine(outputDirectory, "tables.csv");
                    var allColumnsPath = Path.Combine(outputDirectory, "columns.csv");
                    var allRelationsPath = Path.Combine(outputDirectory, "relations.csv");

                    var allTablesContent = _csvConverter.ConvertTablesToCsv(_schemaProvider.Tables);
                    var allColumnsContent = _csvConverter.ConvertColumnsToCsv(_schemaProvider.Columns);
                    var allRelationsContent = _csvConverter.ConvertRelationsToCsv(_schemaProvider.Relations);

                    await Task.WhenAll(
                        File.WriteAllTextAsync(allTablesPath, allTablesContent),
                        File.WriteAllTextAsync(allColumnsPath, allColumnsContent),
                        File.WriteAllTextAsync(allRelationsPath, allRelationsContent)
                    );

                    results.AddRange(new[]
                    {
                        new { type = "tables", path = allTablesPath, record_count = _schemaProvider.Tables.Count },
                        new { type = "columns", path = allColumnsPath, record_count = _schemaProvider.Columns.Count },
                        new { type = "relations", path = allRelationsPath, record_count = _schemaProvider.Relations.Count }
                    });
                    break;

                default:
                    return new
                    {
                        success = false,
                        error = $"Invalid CSV type '{csvType}'. Valid types are: tables, columns, relations, all"
                    };
            }

            return new
            {
                success = true,
                message = $"CSV files generated successfully for type '{normalizedType}'",
                csv_type = normalizedType,
                output_directory = outputDirectory,
                files = results.ToArray(),
                profile = _profileManager.CurrentProfile
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = $"Error occurred while generating CSV files: {ex.Message}"
            };
        }
    }
}