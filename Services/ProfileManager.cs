using Microsoft.Extensions.Logging;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Manages the loading of database schema profiles with dynamic switching capability.
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

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var rootDirectory = Path.Combine(documentsPath, "SqlSchemaBridgeMCP");
        _settingsFile = Path.Combine(rootDirectory, ".current_profile");

        // Load current profile with priority order:
        // 1. Saved profile from settings file
        // 2. Default profile
        _currentProfile = LoadCurrentProfile();

        _logger.LogInformation("Using profile: {CurrentProfile}", _currentProfile);

        // Validate current profile exists
        if (!Directory.Exists(CurrentProfilePath))
        {
            _logger.LogWarning("Current profile directory not found at: {ProfilePath}. Available profiles can be listed using list_available_profiles tool.", CurrentProfilePath);
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
        if (!Directory.Exists(profilePath))
        {
            _logger.LogError("Profile directory not found: {ProfilePath}", profilePath);
            return false;
        }

        try
        {
            // Ensure the settings directory exists
            var settingsDir = Path.GetDirectoryName(_settingsFile);
            if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            // Save the new profile to settings file
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

    public string GetProfileDirectory(string profileName)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "SqlSchemaBridgeMCP", profileName);
    }

    public string GetProfilesRootDirectory()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "SqlSchemaBridgeMCP");
    }
}
