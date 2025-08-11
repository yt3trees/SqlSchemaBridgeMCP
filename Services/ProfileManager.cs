using Microsoft.Extensions.Logging;

namespace SqlSchemaBridgeMCP.Services;

/// <summary>
/// Manages the loading of database schema profiles.
/// </summary>
public class ProfileManager
{
    private readonly ILogger<ProfileManager> _logger;
    public string ProfilePath { get; }
    public string? ProfileReadme { get; }

    public ProfileManager(ILogger<ProfileManager> logger)
    {
        _logger = logger;
        var profileName = Environment.GetEnvironmentVariable("DB_PROFILE");
        if (string.IsNullOrWhiteSpace(profileName))
        {
            Console.WriteLine("Error: The DB_PROFILE environment variable is not set.");
            Console.WriteLine("This variable is required to select the database schema profile.");
            Console.WriteLine();
            Console.WriteLine("You can set it in your shell:");
            Console.WriteLine("  Example (Windows): set DB_PROFILE=ProjectA_DB");
            Console.WriteLine("  Example (Linux/macOS): export DB_PROFILE=ProjectA_DB");
            Console.WriteLine();
            Console.WriteLine("Alternatively, you can configure it in your MCP client's settings file (e.g., .gemini/settings.json):");
            Console.WriteLine(@"{
  ""mcpServers"": {
    ""SqlSchemaBridgeMCP"": {
      ...
      ""env"": {
        ""DB_PROFILE"": ""YourProfileName""
      }
    }
  }
}");
            Console.WriteLine();
            // Exit the application gracefully.
            Environment.Exit(0);
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        ProfilePath = Path.Combine(documentsPath, "SqlSchemaBridgeMCP", profileName);

        _logger.LogInformation("Using profile path: {ProfilePath}", ProfilePath);

        if (!Directory.Exists(ProfilePath))
        {
            _logger.LogError("Profile directory not found at: {ProfilePath}", ProfilePath);
            throw new DirectoryNotFoundException($"The profile directory '{ProfilePath}' was not found. Please ensure it exists and contains the required CSV files.");
        }

        var readmePath = Path.Combine(ProfilePath, "README.md");
        if (File.Exists(readmePath))
        {
            _logger.LogInformation("Loading profile README.md from: {ReadmePath}", readmePath);
            ProfileReadme = File.ReadAllText(readmePath);
        }
        else
        {
            _logger.LogInformation("No README.md found in profile directory. Skipping.");
            ProfileReadme = null;
        }
    }
}
