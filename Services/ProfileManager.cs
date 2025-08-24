using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Manages the loading of database schema profiles with dynamic switching capability.
/// It searches for profiles in multiple locations: the path specified by the SQLSCHEMABRIDGEMCP_PROFILES_PATH environment variable (if set) and the default .SqlSchemaBridgeMCP folder in the user's profile directory.
/// </summary>
public class ProfileManager
{
    private readonly ILogger<ProfileManager> _logger;
    private readonly string _settingsFile;
    private string _currentProfile;
    private string? _currentProfileReadme;

    public string CurrentProfile => _currentProfile;
    public string CurrentProfilePath => GetProfileDirectory(_currentProfile);
    public string? CurrentProfileReadme => _currentProfileReadme;

    public ProfileManager(ILogger<ProfileManager> logger)
    {
        _logger = logger;

        var settingsDirectory = GetDefaultBaseDirectory(); // Settings are always stored in the default location

        // Ensure the settings directory exists.
        if (!Directory.Exists(settingsDirectory))
        {
            try
            {
                Directory.CreateDirectory(settingsDirectory);
                _logger.LogInformation("Created profile settings directory: {Path}", settingsDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create profile settings directory: {Path}", settingsDirectory);
            }
        }

        _settingsFile = Path.Combine(settingsDirectory, ".current_profile");

        // Load current profile with priority order:
        // 1. Saved profile from settings file
        // 2. Default profile
        _currentProfile = LoadCurrentProfile();

        _logger.LogInformation("Using profile: {CurrentProfile}", _currentProfile);

        // Validate current profile exists across all search paths
        if (!Directory.Exists(CurrentProfilePath))
        {
            _logger.LogWarning("Current profile directory not found for '{CurrentProfile}'. Searched paths: {Paths}", _currentProfile, string.Join(", ", GetProfileSearchPaths()));
        }

        LoadProfileReadme();
    }

    private string LoadCurrentProfile()
    {
        // Priority 1: Saved profile from settings file
        if (File.Exists(_settingsFile))
        {
            try
            {
                var savedProfile = File.ReadAllText(_settingsFile).Trim();
                if (!string.IsNullOrEmpty(savedProfile))
                {
                    _logger.LogInformation("Loaded saved profile from settings: {SavedProfile}", savedProfile);
                    return savedProfile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to read saved profile settings: {Error}", ex.Message);
            }
        }

        // Priority 2: Default
        _logger.LogInformation("No profile specified, using default profile");
        return "default";
    }

    public async Task<bool> SwitchProfileAsync(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            _logger.LogError("Profile name cannot be empty");
            return false;
        }

        var profilePath = GetProfileDirectory(profileName);
        // The profile must exist to be switched to. GetProfileDirectory returns a potential path even if it doesn't exist, so we must check.
        if (!Directory.Exists(profilePath))
        {
            _logger.LogError("Profile directory not found for profile '{ProfileName}' in any of the search paths.", profileName);
            return false;
        }

        try
        {
            // Save the new profile to the settings file in the default location.
            await File.WriteAllTextAsync(_settingsFile, profileName);

            _currentProfile = profileName;
            LoadProfileReadme();

            _logger.LogInformation("Successfully switched to profile: {ProfileName}", profileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to switch profile to {ProfileName}: {Error}", profileName, ex.Message);
            return false;
        }
    }

    private void LoadProfileReadme()
    {
        var readmePath = Path.Combine(CurrentProfilePath, "README.md");
        if (File.Exists(readmePath))
        {
            try
            {
                _currentProfileReadme = File.ReadAllText(readmePath);
                _logger.LogInformation("Loaded README.md for profile: {CurrentProfile}", _currentProfile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load README.md for profile {CurrentProfile}: {Error}", _currentProfile, ex.Message);
                _currentProfileReadme = null;
            }
        }
        else
        {
            _currentProfileReadme = null;
            _logger.LogInformation("No README.md found for profile: {CurrentProfile}", _currentProfile);
        }
    }

    /// <summary>
    /// Gets the path for a given profile name by searching in the configured locations.
    /// Priority is given to the path from the environment variable.
    /// If the profile does not exist, it returns the prospective path in the highest-priority location.
    /// </summary>
    public string GetProfileDirectory(string profileName)
    {
        foreach (var baseDir in GetProfileSearchPaths())
        {
            var profilePath = Path.Combine(baseDir, profileName);
            if (Directory.Exists(profilePath))
            {
                return profilePath;
            }
        }

        // If profile is not found, return the path in the highest-priority directory for creation purposes.
        return Path.Combine(GetProfileSearchPaths().First(), profileName);
    }

    /// <summary>
    /// Returns the primary root directory for profiles, used for creating new profiles or other operations.
    /// This is the path from the environment variable if set, otherwise the default user profile location.
    /// </summary>
    public string GetProfilesRootDirectory()
    {
        return GetProfileSearchPaths().First();
    }

    private List<string> GetProfileSearchPaths()
    {
        var paths = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("SQLSCHEMABRIDGEMCP_PROFILES_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            paths.Add(envPath);
        }
        paths.Add(GetDefaultBaseDirectory());
        return paths.Distinct().ToList();
    }

    private string GetDefaultBaseDirectory()
    {
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfilePath, ".SqlSchemaBridgeMCP");
    }

    /// <summary>
    /// Returns the default base directory, which is also used for storing settings.
    /// </summary>
    public string GetBaseDirectory() => GetDefaultBaseDirectory();

    /// <summary>
    /// Gets a list of all available profile names from all configured search paths.
    /// </summary>
    public string[] GetAvailableProfiles()
    {
        var allProfiles = new List<string>();
        var searchPaths = GetProfileSearchPaths();
        _logger.LogInformation("Searching for profiles in the following paths: {Paths}", string.Join(", ", searchPaths));

        foreach (var baseDir in searchPaths)
        {
            if (Directory.Exists(baseDir))
            {
                try
                {
                    var profilesInDir = Directory.GetDirectories(baseDir)
                        .Select(Path.GetFileName)
                        .Where(name => name != null)
                        .Cast<string>()
                        .ToList();
                    
                    _logger.LogInformation("Found {Count} profiles in '{Path}': {Profiles}", profilesInDir.Count, baseDir, string.Join(", ", profilesInDir));
                    allProfiles.AddRange(profilesInDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not access or enumerate directories in path: {Path}", baseDir);
                }
            }
            else
            {
                _logger.LogWarning("Profile search path does not exist: {Path}", baseDir);
            }
        }
        
        var distinctProfiles = allProfiles.Distinct().ToArray();
        _logger.LogInformation("Total unique profiles found: {Count}", distinctProfiles.Length);
        return distinctProfiles;
    }
}