#region File Description
//---------------------------------------------------------------------------
//
// File: OotpDataProvider.cs
// Author: Claude
// Copyright: (C) 2024
// Description: Implementation of IOtpDataProvider that uses the Core IOotpDataService.
//              This adapter bridges the stateless Core service with the stateful MCP provider.
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

using OOTPDatabaseConverter.Core;

namespace OOTPDatabaseConverter.Mcp.Services;

/// <summary>
/// Implementation of <see cref="IOtpDataProvider"/> that uses the Core library's
/// <see cref="IOotpDataService"/> for data access operations.
/// </summary>
/// <remarks>
/// This adapter bridges the stateless Core service (which takes paths as parameters)
/// with the stateful MCP provider (which loads once and maintains state).
/// </remarks>
public class OotpDataProvider : IOtpDataProvider
{
    private readonly IOotpDataService _service;
    private string? _databasePath;
    private bool _isLoaded;
    private List<TableInfo>? _cachedTables;
    private Dictionary<string, int> _tableRowCounts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OotpDataProvider"/> class.
    /// </summary>
    /// <param name="service">The OOTP data service from the Core library.</param>
    public OotpDataProvider(IOotpDataService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public string? DatabasePath => _databasePath;

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    /// <inheritdoc />
    public async Task<bool> LoadDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));
        }

        if (!Directory.Exists(databasePath))
        {
            throw new ArgumentException($"Directory does not exist: {databasePath}", nameof(databasePath));
        }

        // Check for required ODB files
        var requiredFiles = new[]
        {
            "historical_database.odb",
            "historical_minor_database.odb"
        };

        foreach (var file in requiredFiles)
        {
            var fullPath = Path.Combine(databasePath, file);
            if (!File.Exists(fullPath))
            {
                throw new ArgumentException($"Required ODB file not found: {file}", nameof(databasePath));
            }
        }

        try
        {
            // Clear any previously cached data
            await _service.ClearCacheAsync(_databasePath);
            _cachedTables = null;
            _tableRowCounts.Clear();

            // Set the new database path
            _databasePath = databasePath;
            _isLoaded = true;

            // Pre-cache table list and row counts
            await RefreshTableCacheAsync(cancellationToken);

            return true;
        }
        catch (Exception)
        {
            _isLoaded = false;
            _databasePath = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<OdbVersion> DetectVersionAsync()
    {
        EnsureLoaded();
        var mainDbPath = Path.Combine(_databasePath!, "historical_database.odb");
        return await _service.DetectVersionAsync(mainDbPath);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TableInfo>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        EnsureLoaded();

        if (_cachedTables != null)
        {
            return _cachedTables;
        }

        await RefreshTableCacheAsync(cancellationToken);
        return _cachedTables!;
    }

    /// <inheritdoc />
    public async Task<TableSchema?> GetSchemaAsync(string tableName, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        }

        // Find which database file contains this table
        var (odbPath, actualTableName) = await FindTableLocationAsync(tableName, cancellationToken);

        if (odbPath == null)
        {
            return null;
        }

        try
        {
            var columns = await _service.GetSchemaAsync(odbPath, actualTableName);
            var columnList = columns.Select((name, index) => new ColumnInfo
            {
                Name = name,
                Index = index,
                DataType = InferDataType(name)
            }).ToList();

            return new TableSchema
            {
                TableName = tableName,
                Columns = columnList
            };
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<TableRow>> ReadTableAsync(
        string tableName,
        int offset = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        }

        if (offset < 0)
        {
            throw new ArgumentException("Offset must be zero or greater.", nameof(offset));
        }

        if (limit <= 0)
        {
            throw new ArgumentException("Limit must be greater than zero.", nameof(limit));
        }

        // Find which database file contains this table
        var (odbPath, actualTableName) = await FindTableLocationAsync(tableName, cancellationToken);

        if (odbPath == null)
        {
            throw new KeyNotFoundException($"Table '{tableName}' not found in the database.");
        }

        // Get total count from cache or calculate it
        var totalCount = await GetTableRowCountAsync(tableName, odbPath, actualTableName, cancellationToken);

        // Read the data
        var rows = await _service.ReadTableAsync(odbPath, actualTableName, offset, limit);

        var tableRows = rows.Select((row, index) => new TableRow
        {
            RowIndex = offset + index,
            Values = row.Values.ToArray()
        }).ToList();

        return new PagedResult<TableRow>
        {
            Items = tableRows,
            Offset = offset,
            Limit = limit,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public void UnloadDatabase()
    {
        _databasePath = null;
        _isLoaded = false;
        _cachedTables = null;
        _tableRowCounts.Clear();
        _service.ClearCacheAsync().Wait();
    }

    private void EnsureLoaded()
    {
        if (!_isLoaded || string.IsNullOrEmpty(_databasePath))
        {
            throw new InvalidOperationException("No database loaded. Call LoadDatabaseAsync first.");
        }
    }

    private async Task RefreshTableCacheAsync(CancellationToken cancellationToken)
    {
        var tables = new List<TableInfo>();

        // Get tables from main database
        var mainDbPath = Path.Combine(_databasePath!, "historical_database.odb");
        var mainTables = await _service.ListTablesAsync(mainDbPath);
        foreach (var tableName in mainTables)
        {
            tables.Add(new TableInfo
            {
                Name = tableName,
                DisplayName = FormatDisplayName(tableName),
                SourceFile = "historical_database.odb",
                RowCount = 0 // Will be populated on demand
            });
        }

        // Get tables from minor league database
        var minorDbPath = Path.Combine(_databasePath!, "historical_minor_database.odb");
        var minorTables = await _service.ListTablesAsync(minorDbPath);
        foreach (var tableName in minorTables)
        {
            tables.Add(new TableInfo
            {
                Name = $"MiLB_{tableName}",
                DisplayName = $"Minor League {FormatDisplayName(tableName)}",
                SourceFile = "historical_minor_database.odb",
                RowCount = 0
            });
        }

        _cachedTables = tables;
    }

    private async Task<(string? odbPath, string tableName)> FindTableLocationAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        // Check if it's a minor league table
        if (tableName.StartsWith("MiLB_"))
        {
            var actualTableName = tableName.Substring(5);
            var minorDbPath = Path.Combine(_databasePath!, "historical_minor_database.odb");
            var minorTables = await _service.ListTablesAsync(minorDbPath);

            if (minorTables.Contains(actualTableName))
            {
                return (minorDbPath, actualTableName);
            }
        }

        // Check main database
        var mainDbPath = Path.Combine(_databasePath!, "historical_database.odb");
        var mainTables = await _service.ListTablesAsync(mainDbPath);

        if (mainTables.Contains(tableName))
        {
            return (mainDbPath, tableName);
        }

        // Check other database files
        var otherDbFiles = new[]
        {
            "historical_lineups.odb",
            "historical_transactions.odb"
        };

        foreach (var dbFile in otherDbFiles)
        {
            var dbPath = Path.Combine(_databasePath!, dbFile);
            if (File.Exists(dbPath))
            {
                var tables = await _service.ListTablesAsync(dbPath);
                if (tables.Contains(tableName))
                {
                    return (dbPath, tableName);
                }
            }
        }

        return (null, tableName);
    }

    private async Task<int> GetTableRowCountAsync(
        string tableName,
        string odbPath,
        string actualTableName,
        CancellationToken cancellationToken)
    {
        // Check cache first
        if (_tableRowCounts.TryGetValue(tableName, out var count))
        {
            return count;
        }

        // Read all data to count (this is inefficient but the Core service doesn't have a count method)
        // In a production implementation, we'd add a CountAsync method to the service
        var rows = await _service.ReadTableAsync(odbPath, actualTableName, 0, int.MaxValue);
        count = rows.Count();

        _tableRowCounts[tableName] = count;
        return count;
    }

    private static string FormatDisplayName(string tableName)
    {
        // Convert PascalCase/camelCase to Title Case with spaces
        var result = new System.Text.StringBuilder();
        foreach (var c in tableName)
        {
            if (char.IsUpper(c) && result.Length > 0)
            {
                result.Append(' ');
            }
            result.Append(c);
        }
        return result.ToString();
    }

    private static string? InferDataType(string columnName)
    {
        // Infer data type from column name patterns
        var lowerName = columnName.ToLowerInvariant();

        if (lowerName.Contains("id") || lowerName.EndsWith("id"))
            return "string";

        if (lowerName.Contains("year") || lowerName.Contains("season"))
            return "integer";

        if (lowerName.Contains("date"))
            return "date";

        if (lowerName.Contains("avg") || lowerName.Contains("era") || lowerName.Contains("pct") || lowerName.Contains("rate"))
            return "decimal";

        if (lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerName.StartsWith("can"))
            return "boolean";

        // Count columns
        if (lowerName == "g" || lowerName == "ab" || lowerName == "r" || lowerName == "h" ||
            lowerName == "2b" || lowerName == "3b" || lowerName == "hr" || lowerName == "rbi" ||
            lowerName == "sb" || lowerName == "cs" || lowerName == "bb" || lowerName == "so" ||
            lowerName == "ip" || lowerName == "w" || lowerName == "l" || lowerName == "sv")
            return "integer";

        return "string";
    }
}