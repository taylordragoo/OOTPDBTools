using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OOTPDatabaseConverter.Core;
using OOTPDatabaseConverter.Mcp.Services;

namespace OOTPDatabaseConverter.Mcp.Tools;

/// <summary>
/// MCP tools for OOTP database operations.
/// These tools provide read-only access to OOTP database files.
/// </summary>
[McpServerToolType]
public static class OtpDatabaseTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Loads an OOTP database from the specified path.
    /// This must be called before other database operations.
    /// </summary>
    /// <param name="provider">The data provider service (injected).</param>
    /// <param name="databasePath">Path to the directory containing OOTP database files.</param>
    /// <returns>JSON result indicating success or failure.</returns>
    [McpServerTool, Description("Load an OOTP database from a directory path. Must be called before querying data.")]
    public static async Task<string> odb_load_database(
        IOtpDataProvider provider,
        [Description("Path to the directory containing OOTP database files (e.g., /path/to/database/)")] string databasePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Database path is required." }, JsonOptions);
            }

            var success = await provider.LoadDatabaseAsync(databasePath);

            if (success)
            {
                var version = await provider.DetectVersionAsync();
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Database loaded successfully from '{databasePath}'",
                    detectedVersion = version.ToString(),
                    versionDescription = GetVersionDescription(version)
                }, JsonOptions);
            }

            return JsonSerializer.Serialize(new { success = false, error = "Failed to load database. Check the path and try again." }, JsonOptions);
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Invalid database path: {ex.Message}" }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error loading database: {ex.Message}" }, JsonOptions);
        }
    }

    /// <summary>
    /// Lists all tables available in the loaded OOTP database.
    /// </summary>
    /// <param name="provider">The data provider service (injected).</param>
    /// <returns>JSON list of available tables with their metadata.</returns>
    [McpServerTool, Description("List all tables available in the loaded OOTP database.")]
    public static async Task<string> odb_list_tables(IOtpDataProvider provider)
    {
        try
        {
            if (!provider.IsLoaded)
            {
                return JsonSerializer.Serialize(new { success = false, error = "No database loaded. Call odb_load_database first." }, JsonOptions);
            }

            var tables = await provider.ListTablesAsync();

            var result = new
            {
                success = true,
                totalTables = tables.Count,
                tables = tables.Select(t => new
                {
                    name = t.Name,
                    displayName = t.DisplayName,
                    rowCount = t.RowCount,
                    sourceFile = t.SourceFile
                })
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error listing tables: {ex.Message}" }, JsonOptions);
        }
    }

    /// <summary>
    /// Gets the schema (column headers) for a specific table.
    /// </summary>
    /// <param name="provider">The data provider service (injected).</param>
    /// <param name="tableName">The name of the table to get the schema for.</param>
    /// <returns>JSON table schema including column names and types.</returns>
    [McpServerTool, Description("Get the schema (column headers) for a specific table in the OOTP database.")]
    public static async Task<string> odb_get_schema(
        IOtpDataProvider provider,
        [Description("Name of the table to get the schema for (e.g., 'Master', 'Batting', 'Pitching')")] string tableName)
    {
        try
        {
            if (!provider.IsLoaded)
            {
                return JsonSerializer.Serialize(new { success = false, error = "No database loaded. Call odb_load_database first." }, JsonOptions);
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Table name is required." }, JsonOptions);
            }

            var schema = await provider.GetSchemaAsync(tableName);

            if (schema == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Table '{tableName}' not found. Use odb_list_tables to see available tables." }, JsonOptions);
            }

            var result = new
            {
                success = true,
                tableName = schema.TableName,
                columnCount = schema.Columns.Count,
                columns = schema.Columns.Select(c => new
                {
                    name = c.Name,
                    index = c.Index,
                    dataType = c.DataType ?? "unknown"
                })
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error getting schema: {ex.Message}" }, JsonOptions);
        }
    }

    /// <summary>
    /// Reads data from a table with pagination support.
    /// </summary>
    /// <param name="provider">The data provider service (injected).</param>
    /// <param name="tableName">The name of the table to read.</param>
    /// <param name="offset">Zero-based offset for pagination (default: 0).</param>
    /// <param name="limit">Maximum number of rows to return (default: 100, max: 1000).</param>
    /// <returns>JSON table data with pagination info.</returns>
    [McpServerTool, Description("Read data from a table with pagination support.")]
    public static async Task<string> odb_read_table(
        IOtpDataProvider provider,
        [Description("Name of the table to read (e.g., 'Master', 'Batting', 'Pitching')")] string tableName,
        [Description("Zero-based offset for pagination (default: 0)")] int offset = 0,
        [Description("Maximum number of rows to return (default: 100, max: 1000)")] int limit = 100)
    {
        try
        {
            if (!provider.IsLoaded)
            {
                return JsonSerializer.Serialize(new { success = false, error = "No database loaded. Call odb_load_database first." }, JsonOptions);
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Table name is required." }, JsonOptions);
            }

            if (offset < 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Offset must be zero or greater." }, JsonOptions);
            }

            if (limit <= 0 || limit > 1000)
            {
                limit = Math.Clamp(limit, 1, 1000);
            }

            var result = await provider.ReadTableAsync(tableName, offset, limit);

            // Get schema for column names
            var schema = await provider.GetSchemaAsync(tableName);
            var columnNames = schema?.Columns.Select(c => c.Name).ToArray() ?? Array.Empty<string>();

            var response = new
            {
                success = true,
                tableName,
                pagination = new
                {
                    offset = result.Offset,
                    limit = result.Limit,
                    totalCount = result.TotalCount,
                    hasMore = result.HasMore
                },
                rowCount = result.Items.Count,
                columns = columnNames,
                rows = result.Items.Select(r => new
                {
                    rowIndex = r.RowIndex,
                    values = r.Values
                })
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error reading table: {ex.Message}" }, JsonOptions);
        }
    }

    /// <summary>
    /// Detects the OOTP version of the loaded database.
    /// </summary>
    /// <param name="provider">The data provider service (injected).</param>
    /// <returns>JSON information about the detected OOTP version.</returns>
    [McpServerTool, Description("Detect the OOTP version of the loaded database.")]
    public static async Task<string> odb_detect_version(IOtpDataProvider provider)
    {
        try
        {
            if (!provider.IsLoaded)
            {
                return JsonSerializer.Serialize(new { success = false, error = "No database loaded. Call odb_load_database first." }, JsonOptions);
            }

            var version = await provider.DetectVersionAsync();

            var result = new
            {
                success = true,
                version = version.ToString(),
                versionCode = (int)version,
                description = GetVersionDescription(version),
                supported = version != OdbVersion.ODB_Err && version != OdbVersion.ODB_Unk
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error detecting version: {ex.Message}" }, JsonOptions);
        }
    }

    /// <summary>
    /// Unloads the current database and releases resources.
    /// </summary>
    /// <param name="provider">The data provider service (injected).</param>
    /// <returns>JSON result indicating success.</returns>
    [McpServerTool, Description("Unload the current database and release resources.")]
    public static string odb_unload_database(IOtpDataProvider provider)
    {
        try
        {
            provider.UnloadDatabase();
            return JsonSerializer.Serialize(new { success = true, message = "Database unloaded successfully." }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Error unloading database: {ex.Message}" }, JsonOptions);
        }
    }

    /// <summary>
    /// Gets a human-readable description for an OOTP version.
    /// </summary>
    private static string GetVersionDescription(OdbVersion version) => version switch
    {
        OdbVersion.ODB_Err => "Error detecting version - database may be corrupted or unreadable",
        OdbVersion.ODB_17 => "OOTP 17 or 18 format",
        OdbVersion.ODB_19 => "OOTP 19, 20, or 21 format",
        OdbVersion.ODB_22 => "OOTP 22, 23, or 24 format",
        OdbVersion.ODB_25 => "OOTP 25 format",
        OdbVersion.ODB_26 => "OOTP 26 or later format",
        OdbVersion.ODB_Unk => "Unknown version - attempting to process anyway",
        _ => $"Unknown version code: {version}"
    };
}