#region File Description
//---------------------------------------------------------------------------
//
// File: Program.cs
// Author: Steven Leffew
// Copyright: (C) 2024
// Description: Main entry point for OOTP Database MCP Server.
//              Provides MCP tools for querying OOTP database files.
//
//---------------------------------------------------------------------------
#endregion

#region License Info
//---------------------------------------------------------------------------
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//---------------------------------------------------------------------------
#endregion

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using OOTPDatabaseConverter.Core;
using OOTPDatabaseConverter.Mcp.Services;
using OOTPDatabaseConverter.Mcp.Tools;

namespace OOTPDatabaseConverter.Mcp;

/// <summary>
/// Main program class for the OOTP Database MCP Server.
/// </summary>
class Program
{
    /// <summary>
    /// Main entry point for the MCP server.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    static async Task Main(string[] args)
    {
        // Parse command-line arguments
        string? databasePath = null;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-d":
                case "--database":
                    if (i + 1 < args.Length)
                    {
                        databasePath = args[++i];
                    }
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
            }
        }

        if (showHelp)
        {
            ShowHelp();
            return;
        }

        // Build and run the host
        var builder = Host.CreateApplicationBuilder();

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Configure logging level based on environment
        var logLevel = Environment.GetEnvironmentVariable("MCP_LOG_LEVEL")?.ToUpperInvariant() switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Information,
            "WARN" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            _ => LogLevel.Warning
        };
        builder.Logging.SetMinimumLevel(logLevel);

        // Register services
        ConfigureServices(builder.Services, databasePath);

        // Build the host
        var host = builder.Build();

        // Display startup information
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("OOTP Database MCP Server starting...");

        if (!string.IsNullOrEmpty(databasePath))
        {
            logger.LogInformation("Pre-configured database path: {Path}", databasePath);
        }

        // Run the MCP server
        await host.RunAsync();
    }

    /// <summary>
    /// Configures dependency injection services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Optional pre-configured database path.</param>
    private static void ConfigureServices(IServiceCollection services, string? databasePath)
    {
        // Register Core library services
        services.AddSingleton<IOotpDataService, OotpDataService>();

        // Register the MCP data provider (uses Core service)
        services.AddSingleton<IOtpDataProvider, OotpDataProvider>();

        // Register MCP server with tools (auto-discovers [McpServerToolType] classes)
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        // Optionally pre-load a database if path is provided
        if (!string.IsNullOrEmpty(databasePath))
        {
            services.AddHostedService<DatabasePreloadService>(sp =>
            {
                var provider = sp.GetRequiredService<IOtpDataProvider>();
                var logger = sp.GetRequiredService<ILogger<DatabasePreloadService>>();
                return new DatabasePreloadService(provider, logger, databasePath);
            });
        }
    }

    /// <summary>
    /// Displays help information.
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("OOTP Database MCP Server v1.0.0");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine("An MCP (Model Context Protocol) server for querying OOTP database files.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  OOTPDatabaseConverter.Mcp [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -d, --database <path>  Pre-load database from the specified directory");
        Console.WriteLine("  -h, --help             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  MCP_LOG_LEVEL          Set log level (Trace, Debug, Info, Warn, Error)");
        Console.WriteLine();
        Console.WriteLine("MCP Tools Provided:");
        Console.WriteLine("  odb_load_database      Load an OOTP database from a directory");
        Console.WriteLine("  odb_list_tables        List all tables in the loaded database");
        Console.WriteLine("  odb_get_schema         Get column headers for a specific table");
        Console.WriteLine("  odb_read_table         Read table data with pagination");
        Console.WriteLine("  odb_detect_version     Detect the OOTP version of the database");
        Console.WriteLine();
        Console.WriteLine("Example MCP Client Configuration:");
        Console.WriteLine("  {");
        Console.WriteLine("    \"mcpServers\": {");
        Console.WriteLine("      \"ootp-db\": {");
        Console.WriteLine("        \"command\": \"OOTPDatabaseConverter.Mcp\",");
        Console.WriteLine("        \"args\": [\"--database\", \"/path/to/ootp/database\"]");
        Console.WriteLine("      }");
        Console.WriteLine("    }");
        Console.WriteLine("  }");
    }
}

/// <summary>
/// Hosted service that pre-loads a database on startup.
/// </summary>
public class DatabasePreloadService : IHostedService
{
    private readonly IOtpDataProvider _dataProvider;
    private readonly ILogger<DatabasePreloadService> _logger;
    private readonly string _databasePath;

    /// <summary>
    /// Initializes a new instance of the DatabasePreloadService class.
    /// </summary>
    public DatabasePreloadService(
        IOtpDataProvider dataProvider,
        ILogger<DatabasePreloadService> logger,
        string databasePath)
    {
        _dataProvider = dataProvider;
        _logger = logger;
        _databasePath = databasePath;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pre-loading database from: {Path}", _databasePath);

        try
        {
            var success = await _dataProvider.LoadDatabaseAsync(_databasePath, cancellationToken);

            if (success)
            {
                var version = await _dataProvider.DetectVersionAsync();
                _logger.LogInformation("Database pre-loaded successfully. Version: {Version}", version);
            }
            else
            {
                _logger.LogWarning("Failed to pre-load database from: {Path}", _databasePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pre-loading database from: {Path}", _databasePath);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dataProvider.UnloadDatabase();
        return Task.CompletedTask;
    }
}