using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

public class ProfileManagementTools
{
    private readonly ProfileManager _profileManager;
    private readonly SchemaProvider _schemaProvider;

    public ProfileManagementTools(ProfileManager profileManager, SchemaProvider schemaProvider)
    {
        _profileManager = profileManager;
        _schemaProvider = schemaProvider;
    }

    [McpServerTool]
    [Description("Switches to a different profile and reloads schema data")]
    public async Task<object> SwitchProfile(
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
    public Task<object> GetCurrentProfile()
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
    public Task<object> ReloadSchema()
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
}