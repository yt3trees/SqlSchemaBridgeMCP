using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using System.Text.Json;
using System.Net.Sockets;
using System.Net;

namespace SqlSchemaBridgeMCP.Services;

public class WebDebugService
{
    private readonly ILogger<WebDebugService> _logger;
    private readonly ProfileManager _profileManager;
    private WebApplication? _app;
    private int _port;
    private Task? _webServerTask;

    public WebDebugService(ILogger<WebDebugService> logger, ProfileManager profileManager)
    {
        _logger = logger;
        _profileManager = profileManager;
        _port = FindFirstFreePort(24300);
    }

    private int FindFirstFreePort(int startPort)
    {
        for (int port = startPort; port < startPort + 100; port++)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                continue; // Port is in use, try next
            }
        }
        throw new InvalidOperationException($"No free ports available starting from {startPort}");
    }

    public void StartInBackground()
    {
        if (_webServerTask != null && !_webServerTask.IsCompleted)
        {
            _logger.LogWarning("Web debug server is already running");
            return;
        }

        _webServerTask = Task.Run(async () =>
        {
            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start web debug server in background");
            }
        });

        _logger.LogInformation("Web debug interface starting at http://localhost:{Port}", _port);
    }

    public async Task StartAsync()
    {
        if (_app != null)
        {
            _logger.LogWarning("Web debug server is already running");
            return;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://localhost:{_port}");

            _app = builder.Build();

            // Configure routes
            ConfigureRoutes();

            _logger.LogInformation("Web debug server started at http://localhost:{Port}", _port);
            _logger.LogInformation("Dashboard available at: http://localhost:{Port}", _port);
         // Start and keep the server running
            await _app.RunAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start web debug server on port {Port}", _port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
            _logger.LogInformation("Web debug server stopped");
        }
    }

    private void ConfigureRoutes()
    {
        if (_app == null) return;

        // Serve static HTML interface
        _app.MapGet("/", async context =>
        {
            var html = GenerateIndexHtml();
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });

        // List all profiles
        _app.MapGet("/profiles", async context =>
        {
            try
            {
                var profiles = _profileManager.GetAvailableProfiles();
                var currentProfile = _profileManager.CurrentProfile;
 var result = new
                {
                    current_profile = currentProfile,
                    available_profiles = profiles.Select(p => new { name = p }).ToArray()
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing profiles");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Error: {ex.Message}");
            }
        });

        // List files in a profile
        _app.MapGet("/profiles/{profileName}/files", async (string profileName, HttpContext context) =>
        {
            try
            {
                var profilePath = Path.Combine(_profileManager.GetBaseDirectory(), profileName);
                if (!Directory.Exists(profilePath))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync($"Profile '{profileName}' not found");
                    return;
                }

                var files = Directory.GetFiles(profilePath, "*.csv")
                    .Select(f => new
                    {
                        name = Path.GetFileName(f),
                        size = new FileInfo(f).Length,
                        modified = new FileInfo(f).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToArray();

                var result = new
                {
                    profile = profileName,
                    files = files
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files for profile {ProfileName}", profileName);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Error: {ex.Message}");
            }
        });

        // Download CSV file
        _app.MapGet("/profiles/{profileName}/files/{fileName}", async (string profileName, string fileName, HttpContext context) =>
        {
            try
            {
                var profilePath = Path.Combine(_profileManager.GetBaseDirectory(), profileName);
                var filePath = Path.Combine(profilePath, fileName);
 if (!File.Exists(filePath) || !fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync($"File '{fileName}' not found in profile '{profileName}'");
                    return;
                }

                var contentType = "text/csv; charset=utf-8";
                var content = await File.ReadAllTextAsync(filePath);
 context.Response.ContentType = contentType;
                context.Response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                await context.Response.WriteAsync(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving file {FileName} from profile {ProfileName}", fileName, profileName);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Error: {ex.Message}");
            }
        });

        // View logs (mock endpoint - in production you'd connect to actual logging)
        _app.MapGet("/logs", async context =>
        {
            try
            {
                // This is a simple mock - in a real implementation, you'd read from actual log files
                var logs = new[]
                {
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] Web debug server is running",
                    $"{DateTime.Now.AddMinutes(-1):yyyy-MM-dd HH:mm:ss} [INFO] Current profile: {_profileManager.CurrentProfile}",
                    $"{DateTime.Now.AddMinutes(-2):yyyy-MM-dd HH:mm:ss} [DEBUG] Profile directory: {_profileManager.CurrentProfilePath}",
                };

                var result = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    logs = logs
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Error: {ex.Message}");
            }
        });
    }

    private string GenerateIndexHtml()
    {
        return """
<!DOCTYPE html>
<html>
<head>
    <title>SqlSchemaBridgeMCP Debug Interface</title>
    <meta charset="utf-8">
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; }
        .card { background: white; border-radius: 8px; padding: 20px; margin: 10px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .header { text-align: center; color: #333; }
        .section { margin: 20px 0; }
        .btn { background: #007cba; color: white; padding: 10px 15px; border: none; border-radius: 4px; cursor: pointer; margin: 5px; text-decoration: none; display: inline-block; }
        .btn:hover { background: #005a87; }
        .grid { display: grid; grid-template-columns: 1fr 2fr; gap: 20px; }
        .grid-bottom { display: grid; grid-template-columns: 1fr; gap: 20px; margin-top: 20px; }
        @media (max-width: 768px) {
            .grid { grid-template-columns: 1fr; }
        }
        pre { background: #f8f8f8; padding: 15px; border-radius: 4px; overflow-x: auto; white-space: pre-wrap; }
        .status { padding: 5px 10px; border-radius: 4px; margin: 5px 0; }
        .info { background: #d1ecf1; color: #0c5460; }
        .loading { color: #666; font-style: italic; }
        table { width: 100%; border-collapse: collapse; margin: 10px 0; table-layout: fixed; }
        th, td { padding: 6px 8px; text-align: left; border: 1px solid #ddd; word-wrap: break-word; overflow: hidden; text-overflow: ellipsis; }
        th { background: #f8f9fa; font-weight: bold; }
        tr:hover { background: #f8f9fa; }
        .actions-column { width: 140px; min-width: 140px; }
        .name-column { width: 35%; }
        .size-column { width: 70px; text-align: right; font-size: 12px; }
        .date-column { width: 120px; font-size: 11px; }
        .btn-small {
            padding: 4px 8px;
            margin: 1px;
            font-size: 11px;
            display: inline-block;
            text-decoration: none;
            border-radius: 3px;
            white-space: nowrap;
        }
        .actions-column .btn-small {
            margin: 2px 0;
            text-align: center;
        }
        .csv-table { max-height: 400px; overflow: auto; border: 1px solid #ddd; margin: 10px 0; }
        .csv-table table { margin: 0; table-layout: auto; }
        .csv-table th { position: sticky; top: 0; background: #f8f9fa; z-index: 1; }
    </style>
</head>
<body>
    <div class="container">
        <div class="card">
            <h1 class="header">SqlSchemaBridgeMCP Debug Interface</h1>
            <div class="status info">
                Server running at http://localhost:
"""
                + _port +
"""
            </div>
        </div>

        <div class="grid">
            <div class="card">
                <h2>Profiles</h2>
                <p>Current profiles and CSV data files</p>
                <button class="btn" onclick="loadProfiles()">Load Profiles</button>
                <div id="profiles-content" class="loading">Click "Load Profiles" to view...</div>
            </div>

            <div class="card">
                <h2>CSV Files</h2>
                <p>Browse and view CSV data files</p>
                <div id="profile-select"></div>
                <div id="files-content" class="loading">Select a profile to view files...</div>
            </div>
        </div>

        <div class="grid-bottom">
            <div class="card">
                <h2>Logs</h2>
                <p>Application logs and debug information</p>
                <button class="btn" onclick="loadLogs()">Load Logs</button>
                <button class="btn" onclick="autoRefreshLogs()">Auto Refresh</button>
                <div id="logs-content" class="loading">Click "Load Logs" to view...</div>
            </div>
        </div>
    </div>

    <script>
        let autoRefresh = false;

        async function loadProfiles() {
            const container = document.getElementById('profiles-content');
            container.innerHTML = '<div class="loading">Loading...</div>';

            try {
                const response = await fetch('/profiles');
                const data = await response.json();

                let html = `<div class="status info">Current Profile: ${data.current_profile}</div>`;
                html += '<table><tr><th>Profile Name</th><th>Actions</th></tr>';

                data.available_profiles.forEach(profile => {
                    const isCurrent = profile.name === data.current_profile;
                    html += `<tr ${isCurrent ? 'style="background: #e7f3ff;"' : ''}>
                        <td>${profile.name} ${isCurrent ? '(current)' : ''}</td>
                        <td><button class="btn" onclick="loadProfileFiles('${profile.name}')">View Files</button></td>
                    </tr>`;
                });
                html += '</table>';

                // Update profile selector
                const profileSelect = document.getElementById('profile-select');
                let selectHtml = '<select id="profile-selector" onchange="onProfileChange()">';
                selectHtml += '<option value="">Select a profile...</option>';
                data.available_profiles.forEach(profile => {
                    selectHtml += `<option value="${profile.name}">${profile.name}</option>`;
                });
                selectHtml += '</select>';
                profileSelect.innerHTML = selectHtml;

                container.innerHTML = html;
            } catch (error) {
                container.innerHTML = `<div class="status" style="background: #f8d7da; color: #721c24;">Error: ${error.message}</div>`;
            }
        }

        async function loadProfileFiles(profileName) {
            const container = document.getElementById('files-content');
            container.innerHTML = '<div class="loading">Loading...</div>';
         try {
                const response = await fetch(`/profiles/${profileName}/files`);
                const data = await response.json();
 let html = `<div class="status info">Files in profile: ${data.profile}</div>`;
 if (data.files.length === 0) {
                    html += '<p>No CSV files found in this profile.</p>';
                } else {
                    html += '<table><tr><th class="name-column">File Name</th><th class="size-column">Size (bytes)</th><th class="date-column">Modified</th><th class="actions-column">Actions</th></tr>';
                    data.files.forEach(file => {
                        html += `<tr>
                            <td class="name-column">${file.name}</td>
                            <td class="size-column">${file.size}</td>
                            <td class="date-column">${file.modified}</td>
                            <td class="actions-column">
                                <button class="btn btn-small" onclick="viewFileAsTable('${data.profile}', '${file.name}')">View Table</button>
                                <a class="btn btn-small" href="/profiles/${data.profile}/files/${file.name}" target="_blank">Download</a>
                            </td>
                        </tr>`;
                    });
                    html += '</table>';
                }
 container.innerHTML = html;
 // Update the profile selector
                document.getElementById('profile-selector').value = profileName;
            } catch (error) {
                container.innerHTML = `<div class="status" style="background: #f8d7da; color: #721c24;">Error: ${error.message}</div>`;
            }
        }

        function onProfileChange() {
            const select = document.getElementById('profile-selector');
            if (select.value) {
                loadProfileFiles(select.value);
            }
        }

        async function viewFileAsTable(profileName, fileName) {
            try {
                const response = await fetch(`/profiles/${profileName}/files/${fileName}`);
                const content = await response.text();
 // Parse CSV content
                const lines = content.trim().split('\n');
                if (lines.length === 0) {
                    alert('File is empty');
                    return;
                }
 const headers = lines[0].split(',').map(h => h.trim());
                const rows = lines.slice(1).map(line => line.split(',').map(cell => cell.trim()));
 // Generate HTML table
                let tableHtml = '<div class="csv-table"><table>';
                tableHtml += '<tr>';
                headers.forEach(header => {
                    tableHtml += `<th>${escapeHtml(header)}</th>`;
                });
                tableHtml += '</tr>';
 rows.forEach(row => {
                    tableHtml += '<tr>';
                    headers.forEach((_, index) => {
                        const cellValue = row[index] || '';
                        tableHtml += `<td>${escapeHtml(cellValue)}</td>`;
                    });
                    tableHtml += '</tr>';
                });
                tableHtml += '</table></div>';
 const popup = window.open('', '_blank', 'width=1000,height=700');
                popup.document.write(`
                    <html>
                        <head>
                            <title>${fileName} - ${profileName}</title>
                            <style>
                                body { font-family: Arial, sans-serif; margin: 20px; }
                                .header { background: #f8f9fa; padding: 15px; border: 1px solid #dee2e6; margin-bottom: 15px; border-radius: 4px; }
                                .csv-table { max-height: 500px; overflow: auto; border: 1px solid #ddd; }
                                table { width: 100%; border-collapse: collapse; }
                                th, td { padding: 8px 12px; text-align: left; border: 1px solid #ddd; }
                                th { background: #f8f9fa; font-weight: bold; position: sticky; top: 0; z-index: 1; }
                                tr:nth-child(even) { background: #f9f9f9; }
                                tr:hover { background: #e8f4fd; }
                                .stats { margin-top: 10px; font-size: 14px; color: #666; }
                            </style>
                        </head>
                        <body>
                            <div class="header">
                                <h3>${fileName}</h3>
                                <p>Profile: ${profileName}</p>
                                <div class="stats">
                                    <strong>Columns:</strong> ${headers.length} | 
                                    <strong>Rows:</strong> ${rows.length} | 
                                    <strong>File Size:</strong> ${content.length} characters
                                </div>
                            </div>
                            ${tableHtml}
                        </body>
                    </html>
                `);
            } catch (error) {
                alert(`Error loading file: ${error.message}`);
            }
        }
 function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        async function loadLogs() {
            const container = document.getElementById('logs-content');
            container.innerHTML = '<div class="loading">Loading...</div>';
         try {
                const response = await fetch('/logs');
                const data = await response.json();
 let html = `<div class="status info">Logs as of: ${data.timestamp}</div>`;
                html += '<pre>';
                data.logs.forEach(log => {
                    html += log + '\n';
                });
                html += '</pre>';
 container.innerHTML = html;
            } catch (error) {
                container.innerHTML = `<div class="status" style="background: #f8d7da; color: #721c24;">Error: ${error.message}</div>`;
            }
        }

        function autoRefreshLogs() {
            autoRefresh = !autoRefresh;
            const button = event.target;
         if (autoRefresh) {
                button.textContent = 'Stop Auto Refresh';
                button.style.background = '#dc3545';
                const interval = setInterval(() => {
                    if (!autoRefresh) {
                        clearInterval(interval);
                        return;
                    }
                    loadLogs();
                }, 3000);
            } else {
                button.textContent = 'Auto Refresh';
                button.style.background = '#007cba';
            }
        }

        // Load profiles on page load
        window.onload = function() {
            loadProfiles();
        };
    </script>
</body>
</html>
""";
    }
}