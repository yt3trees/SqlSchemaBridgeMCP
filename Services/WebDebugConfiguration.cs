namespace SqlSchemaBridgeMCP.Services;

public class WebDebugConfiguration
{
    public bool EnableWebDebugInterface { get; set; } = true;
    public int DefaultPort { get; set; } = 24300;
    public bool AutoStartWithMCP { get; set; } = true;

    public static WebDebugConfiguration FromEnvironmentVariables()
    {
        var config = new WebDebugConfiguration();

        // Check environment variables
        if (bool.TryParse(Environment.GetEnvironmentVariable("SQLSCHEMA_ENABLE_WEB_DEBUG"), out bool enableWeb))
        {
            config.EnableWebDebugInterface = enableWeb;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("SQLSCHEMA_WEB_DEBUG_PORT"), out int port))
        {
            config.DefaultPort = port;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("SQLSCHEMA_AUTO_START_WEB"), out bool autoStart))
        {
            config.AutoStartWithMCP = autoStart;
        }

        return config;
    }

    public static WebDebugConfiguration FromCommandLineArgs(string[] args)
    {
        var config = FromEnvironmentVariables();

        // Override with command line arguments
        if (args.Contains("--disable-web-debug"))
        {
            config.EnableWebDebugInterface = false;
        }

        if (args.Contains("--enable-web-debug"))
        {
            config.EnableWebDebugInterface = true;
        }

        var portIndex = Array.IndexOf(args, "--web-debug-port");
        if (portIndex >= 0 && portIndex + 1 < args.Length && int.TryParse(args[portIndex + 1], out int port))
        {
            config.DefaultPort = port;
        }

        return config;
    }
}