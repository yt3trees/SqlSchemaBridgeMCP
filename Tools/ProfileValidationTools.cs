using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlSchemaBridgeMCP.Services;

namespace SqlSchemaBridgeMCP.Tools;

public class ProfileValidationTools
{
    private readonly ProfileValidationService _validationService;
    private readonly ProfileManager _profileManager;

    public ProfileValidationTools(ProfileValidationService validationService, ProfileManager profileManager)
    {
        _validationService = validationService;
        _profileManager = profileManager;
    }

    [McpServerTool]
    [Description("Validates CSV file settings for the specified profile")]
    public async Task<object> ValidateProfile(
        [Description("Profile name to validate. If omitted, validates the current profile")] 
        string? profileName = null)
    {
        try
        {
            var targetProfile = profileName ?? _profileManager.CurrentProfile;
            var result = await _validationService.ValidateProfileAsync(targetProfile);

            return new
            {
                profile_name = result.ProfileName,
                is_valid = !result.HasErrors,
                has_warnings = result.HasWarnings,
                summary = result.GetSummary(),
                detailed_report = result.GetDetailedReport(),
                errors = result.Messages.Where(m => m.Type == MessageType.Error).Select(m => m.Message).ToArray(),
                warnings = result.Messages.Where(m => m.Type == MessageType.Warning).Select(m => m.Message).ToArray(),
                info = result.Messages.Where(m => m.Type == MessageType.Info).Select(m => m.Message).ToArray()
            };
        }
        catch (Exception ex)
        {
            return new
            {
                profile_name = profileName ?? "unknown",
                is_valid = false,
                error = $"Error occurred during validation: {ex.Message}"
            };
        }
    }

    [McpServerTool]
    [Description("Gets a list of available profiles")]
    public Task<object> ListAvailableProfiles()
    {
        try
        {
            var profilesDirectory = _profileManager.GetProfilesRootDirectory();

            if (!Directory.Exists(profilesDirectory))
            {
                return Task.FromResult<object>(new
                {
                    profiles = Array.Empty<object>(),
                    message = $"Profile directory does not exist: {profilesDirectory}"
                });
            }

            var profiles = new List<object>();
            var directories = Directory.GetDirectories(profilesDirectory);

            foreach (var dir in directories)
            {
                var profileName = Path.GetFileName(dir);
                var hasTablesFile = File.Exists(Path.Combine(dir, "tables.csv"));
                var hasColumnsFile = File.Exists(Path.Combine(dir, "columns.csv"));
                var hasRelationsFile = File.Exists(Path.Combine(dir, "relations.csv"));
                var isComplete = hasTablesFile && hasColumnsFile && hasRelationsFile;

                profiles.Add(new
                {
                    name = profileName,
                    path = dir,
                    is_complete = isComplete,
                    files = new
                    {
                        tables_csv = hasTablesFile,
                        columns_csv = hasColumnsFile,
                        relations_csv = hasRelationsFile
                    }
                });
            }

            return Task.FromResult<object>(new
            {
                profiles_directory = profilesDirectory,
                current_profile = _profileManager.CurrentProfile,
                profiles = profiles.ToArray()
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object>(new
            {
                error = $"Error occurred while retrieving profile list: {ex.Message}"
            });
        }
    }

    [McpServerTool]
    [Description("Validates all available profiles")]
    public async Task<object> ValidateAllProfiles()
    {
        try
        {
            var profilesDirectory = _profileManager.GetProfilesRootDirectory();

            if (!Directory.Exists(profilesDirectory))
            {
                return new
                {
                    error = $"Profile directory does not exist: {profilesDirectory}"
                };
            }

            var results = new List<object>();
            var directories = Directory.GetDirectories(profilesDirectory);

            foreach (var dir in directories)
            {
                var profileName = Path.GetFileName(dir);
                var result = await _validationService.ValidateProfileAsync(profileName);

                results.Add(new
                {
                    profile_name = result.ProfileName,
                    is_valid = !result.HasErrors,
                    has_warnings = result.HasWarnings,
                    summary = result.GetSummary(),
                    error_count = result.Messages.Count(m => m.Type == MessageType.Error),
                    warning_count = result.Messages.Count(m => m.Type == MessageType.Warning),
                    errors = result.Messages.Where(m => m.Type == MessageType.Error).Select(m => m.Message).ToArray()
                });
            }

            var totalProfiles = results.Count;
            var validProfiles = results.Count(r => (bool)r.GetType().GetProperty("is_valid")!.GetValue(r)!);
            var profilesWithWarnings = results.Count(r => (bool)r.GetType().GetProperty("has_warnings")!.GetValue(r)!);

            return new
            {
                summary = $"Validation completed: {validProfiles} of {totalProfiles} profiles are valid, {profilesWithWarnings} have warnings",
                total_profiles = totalProfiles,
                valid_profiles = validProfiles,
                profiles_with_warnings = profilesWithWarnings,
                results = results.ToArray()
            };
        }
        catch (Exception ex)
        {
            return new
            {
                error = $"Error occurred during all profiles validation: {ex.Message}"
            };
        }
    }
}