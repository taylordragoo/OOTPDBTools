#region File Description
//---------------------------------------------------------------------------
//
// File: OotpDataService.cs
// Author: Claude
// Copyright: (C) 2024
// Description: Implementation of OOTP Database data access operations.
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

#region Using Statements
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace OOTPDatabaseConverter.Core
{
    /// <summary>
    /// Implementation of OOTP Database data access operations.
    /// Provides structured access to ODB files with caching support.
    /// </summary>
    public class OotpDataService : IOotpDataService
    {
        #region Members
        private readonly string _cacheBasePath;
        private readonly ConcurrentDictionary<string, OdbCacheEntry> _cache;
        private static readonly object _conversionLock = new object();
        #endregion

        #region Nested Classes
        private class OdbCacheEntry
        {
            public string CacheDirectory { get; set; } = string.Empty;
            public OdbVersion Version { get; set; }
            public DateTime LastModified { get; set; }
            public Dictionary<string, string> TableNameMap { get; set; } = new Dictionary<string, string>();
            public int TableCount { get; set; }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes a new instance of the OotpDataService class.
        /// Uses the system temp directory for caching by default.
        /// </summary>
        public OotpDataService() : this(Path.Combine(Path.GetTempPath(), "OOTPDBCache"))
        {
        }

        /// <summary>
        /// Initializes a new instance of the OotpDataService class with a custom cache path.
        /// </summary>
        /// <param name="cachePath">Directory path for caching converted CSV files.</param>
        public OotpDataService(string cachePath)
        {
            _cacheBasePath = cachePath;
            _cache = new ConcurrentDictionary<string, OdbCacheEntry>(StringComparer.OrdinalIgnoreCase);

            // Ensure cache directory exists
            if (!Directory.Exists(_cacheBasePath))
            {
                Directory.CreateDirectory(_cacheBasePath);
            }
        }
        #endregion

        #region Public Methods
        /// <inheritdoc/>
        public async Task<IEnumerable<string>> ListTablesAsync(string odbPath)
        {
            ValidateOdbPath(odbPath);

            var entry = await GetOrCreateCacheEntryAsync(odbPath);
            return entry.TableNameMap.Keys.AsEnumerable();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<string>> GetSchemaAsync(string odbPath, string tableName)
        {
            ValidateOdbPath(odbPath);

            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
            }

            var entry = await GetOrCreateCacheEntryAsync(odbPath);

            // Find the actual CSV file name
            if (!entry.TableNameMap.TryGetValue(tableName, out var csvFileName))
            {
                // Try with .csv extension
                var tableNameWithExtension = tableName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? tableName
                    : tableName + ".csv";

                if (!entry.TableNameMap.Any(kvp => kvp.Value.Equals(tableNameWithExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new KeyNotFoundException($"Table '{tableName}' not found in database. Available tables: {string.Join(", ", entry.TableNameMap.Keys.Take(10))}...");
                }

                csvFileName = entry.TableNameMap.First(kvp => kvp.Value.Equals(tableNameWithExtension, StringComparison.OrdinalIgnoreCase)).Value;
            }

            var csvPath = Path.Combine(entry.CacheDirectory, csvFileName);

            if (!File.Exists(csvPath))
            {
                throw new KeyNotFoundException($"Table file '{csvFileName}' not found in cache.");
            }

            // Read just the header line
            using var reader = new StreamReader(csvPath, Encoding.ASCII);
            var headerLine = await reader.ReadLineAsync();

            if (string.IsNullOrEmpty(headerLine))
            {
                return Enumerable.Empty<string>();
            }

            return ParseCsvLine(headerLine);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Dictionary<string, string>>> ReadTableAsync(string odbPath, string tableName, int offset = 0, int limit = 1000)
        {
            ValidateOdbPath(odbPath);

            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
            }

            if (offset < 0)
            {
                throw new ArgumentException("Offset cannot be negative.", nameof(offset));
            }

            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than zero.", nameof(limit));
            }

            var entry = await GetOrCreateCacheEntryAsync(odbPath);

            // Find the actual CSV file name
            string csvFileName;
            if (!entry.TableNameMap.TryGetValue(tableName, out var foundFileName))
            {
                // Try matching by file name
                var tableNameWithExtension = tableName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? tableName
                    : tableName + ".csv";

                var matchingEntry = entry.TableNameMap.FirstOrDefault(kvp =>
                    kvp.Value.Equals(tableNameWithExtension, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals(tableName, StringComparison.OrdinalIgnoreCase));

                if (matchingEntry.Equals(default(KeyValuePair<string, string>)))
                {
                    throw new KeyNotFoundException($"Table '{tableName}' not found in database.");
                }

                csvFileName = matchingEntry.Value;
            }
            else
            {
                csvFileName = foundFileName;
            }

            var csvPath = Path.Combine(entry.CacheDirectory, csvFileName);

            if (!File.Exists(csvPath))
            {
                throw new KeyNotFoundException($"Table file '{csvFileName}' not found in cache.");
            }

            return await ReadCsvAsDictionariesAsync(csvPath, offset, limit);
        }

        /// <inheritdoc/>
        public async Task<OdbVersion> DetectVersionAsync(string odbPath)
        {
            ValidateOdbPath(odbPath);

            var entry = await GetOrCreateCacheEntryAsync(odbPath);
            return entry.Version;
        }

        /// <inheritdoc/>
        public async Task<string> GetConfigAsync(string odbPath)
        {
            ValidateOdbPath(odbPath);

            var entry = await GetOrCreateCacheEntryAsync(odbPath);

            var sb = new StringBuilder();
            sb.AppendLine($"File: {odbPath}");
            sb.AppendLine($"Version: {entry.Version}");
            sb.AppendLine($"Table Count: {entry.TableCount}");
            sb.AppendLine($"Cache Directory: {entry.CacheDirectory}");
            sb.AppendLine($"Last Modified: {entry.LastModified:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Tables:");

            foreach (var table in entry.TableNameMap.OrderBy(t => t.Key))
            {
                sb.AppendLine($"  - {table.Key} ({table.Value})");
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public Task ClearCacheAsync(string? odbPath = null)
        {
            if (string.IsNullOrEmpty(odbPath))
            {
                // Clear all cache
                foreach (var entry in _cache.Values)
                {
                    try
                    {
                        if (Directory.Exists(entry.CacheDirectory))
                        {
                            Directory.Delete(entry.CacheDirectory, recursive: true);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                _cache.Clear();
            }
            else
            {
                var normalizedPath = Path.GetFullPath(odbPath);
                if (_cache.TryRemove(normalizedPath, out var entry))
                {
                    try
                    {
                        if (Directory.Exists(entry.CacheDirectory))
                        {
                            Directory.Delete(entry.CacheDirectory, recursive: true);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }

            return Task.CompletedTask;
        }
        #endregion

        #region Private Methods
        private void ValidateOdbPath(string odbPath)
        {
            if (string.IsNullOrWhiteSpace(odbPath))
            {
                throw new ArgumentException("ODB path cannot be null or empty.", nameof(odbPath));
            }

            if (!File.Exists(odbPath))
            {
                throw new FileNotFoundException($"ODB file not found: {odbPath}");
            }

            if (!odbPath.EndsWith(".odb", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"File must have .odb extension: {odbPath}", nameof(odbPath));
            }
        }

        private async Task<OdbCacheEntry> GetOrCreateCacheEntryAsync(string odbPath)
        {
            var normalizedPath = Path.GetFullPath(odbPath);
            var lastModified = File.GetLastWriteTimeUtc(odbPath);

            // Check if we have a valid cached entry
            if (_cache.TryGetValue(normalizedPath, out var entry))
            {
                if (entry.LastModified == lastModified && Directory.Exists(entry.CacheDirectory))
                {
                    return entry;
                }

                // Cache is stale, remove it
                _cache.TryRemove(normalizedPath, out _);
            }

            // Create new cache entry
            return await CreateCacheEntryAsync(normalizedPath, lastModified);
        }

        private async Task<OdbCacheEntry> CreateCacheEntryAsync(string odbPath, DateTime lastModified)
        {
            var normalizedPath = Path.GetFullPath(odbPath);

            // Create unique cache directory based on file hash
            var cacheDirectoryName = $"odb_{Math.Abs(normalizedPath.GetHashCode())}_{lastModified.Ticks}";
            var cacheDirectory = Path.Combine(_cacheBasePath, cacheDirectoryName);

            // Ensure directory exists
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            // Detect version and convert to CSV
            var (version, tableCount, tableNames) = await ConvertOdbToCsvAsync(odbPath, cacheDirectory);

            var entry = new OdbCacheEntry
            {
                CacheDirectory = cacheDirectory,
                Version = version,
                LastModified = lastModified,
                TableCount = tableCount,
                TableNameMap = tableNames
            };

            _cache[normalizedPath] = entry;
            return entry;
        }

        private async Task<(OdbVersion version, int tableCount, Dictionary<string, string> tableNames)> ConvertOdbToCsvAsync(string odbPath, string outputDirectory)
        {
            return await Task.Run(() =>
            {
                OdbVersion odbVersion = OdbVersion.ODB_Err;
                int tableCount = 0;
                var tableNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                lock (_conversionLock)
                {
                    try
                    {
                        // Detect version by reading the file
                        odbVersion = DetectOdbVersion(odbPath, out tableCount);

                        // Determine file type based on file name
                        var fileName = Path.GetFileName(odbPath).ToLowerInvariant();

                        string[] csvFileNames;
                        if (fileName.Contains("minor"))
                        {
                            // Minor league database
                            var fileNames = new FileNames(odbVersion);
                            csvFileNames = fileNames.HistoricalMinorDatabaseAllCsvFileNames;
                        }
                        else if (fileName.Contains("lineup"))
                        {
                            // Lineups database - single table
                            csvFileNames = new[] { "Lineups.csv" };
                        }
                        else if (fileName.Contains("transaction"))
                        {
                            // Transactions database - single table
                            csvFileNames = new[] { "Transactions.csv" };
                        }
                        else if (fileName.Contains("stats"))
                        {
                            // Stats database
                            csvFileNames = new[] { "Stats.csv" };
                        }
                        else
                        {
                            // Main historical database
                            var fileNames = new FileNames(odbVersion);
                            csvFileNames = fileNames.HistoricalDatabaseAllCsvFileNames;
                        }

                        // Convert the ODB file to CSV files
                        ConvertOdbToCsvFiles(odbPath, outputDirectory, csvFileNames, tableCount);

                        // Build table name map
                        for (int i = 0; i < csvFileNames.Length && i < tableCount; i++)
                        {
                            if (!string.IsNullOrEmpty(csvFileNames[i]))
                            {
                                var csvName = csvFileNames[i];
                                var tableName = Path.GetFileNameWithoutExtension(csvName);
                                tableNames[tableName] = csvName;
                            }
                        }

                        // Also add any CSV files that were actually created
                        foreach (var csvFile in Directory.GetFiles(outputDirectory, "*.csv"))
                        {
                            var csvName = Path.GetFileName(csvFile);
                            var tableName = Path.GetFileNameWithoutExtension(csvFile);
                            if (!tableNames.ContainsKey(tableName))
                            {
                                tableNames[tableName] = csvName;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to convert ODB file: {ex.Message}", ex);
                    }
                }

                return (odbVersion, tableCount, tableNames);
            });
        }

        private OdbVersion DetectOdbVersion(string odbPath, out int tableCount)
        {
            tableCount = 0;
            var odbVersion = OdbVersion.ODB_Err;
            int odbBytePosition = 0;
            int odbFileSize = 0;
            byte odbTable = 0;
            bool valueChecked = false;

            using (var inputStream = new FileStream(odbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(inputStream, Encoding.ASCII))
            {
                odbFileSize = (int)reader.BaseStream.Length;

                byte currentTable = 0;
                string databaseLine = string.Empty;
                string? checkValue = null;

                // Skip first four bytes (file header)
                while (odbBytePosition < 5)
                {
                    reader.ReadByte();
                    odbBytePosition++;
                }

                while (odbBytePosition < odbFileSize)
                {
                    currentTable = reader.ReadByte();

                    if (odbTable != currentTable)
                    {
                        odbTable = currentTable;
                    }

                    int stringLength = reader.ReadByte() + (reader.ReadByte() * 256);
                    odbBytePosition += 3;

                    // Try to detect version from table 6 (fielding data)
                    if (odbTable == 6 && !valueChecked)
                    {
                        databaseLine = string.Empty;
                        for (int i = 0; i < stringLength; i++)
                        {
                            databaseLine += reader.ReadChar();
                            odbBytePosition++;
                        }

                        var parts = databaseLine.Split('\t');
                        if (parts.Length > 3)
                        {
                            checkValue = parts[3];

                            if (checkValue.Equals("Glf", StringComparison.OrdinalIgnoreCase))
                            {
                                odbVersion = OdbVersion.ODB_22;
                            }
                            else if (checkValue.Equals("teamID", StringComparison.OrdinalIgnoreCase))
                            {
                                odbVersion = OdbVersion.ODB_17;
                            }
                        }

                        valueChecked = true;
                    }
                    else
                    {
                        // Check for OOTP 19/20/21 (25 tables) vs OOTP 17/18 (22 tables)
                        if (odbTable == 22 && odbVersion != OdbVersion.ODB_22)
                        {
                            odbVersion = OdbVersion.ODB_19;
                        }

                        // Skip the data
                        for (int i = 0; i < stringLength; i++)
                        {
                            reader.ReadChar();
                            odbBytePosition++;
                        }
                    }
                }

                // Check for OOTP 25 and 26
                if (odbTable > 25)
                {
                    if (odbTable == 26)
                        odbVersion = OdbVersion.ODB_25;
                    else if (odbTable == 29)
                        odbVersion = OdbVersion.ODB_26;
                    else
                        odbVersion = OdbVersion.ODB_Unk;
                }

                tableCount = odbTable + 1;
            }

            return odbVersion;
        }

        private void ConvertOdbToCsvFiles(string odbPath, string outputDirectory, string[] csvFileNames, int expectedTableCount)
        {
            int odbBytePosition = 0;
            int odbFileSize = 0;
            byte odbTable = 0;
            StreamWriter? outputStream = null;

            try
            {
                using (var inputStream = new FileStream(odbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(inputStream, Encoding.ASCII))
                {
                    odbFileSize = (int)reader.BaseStream.Length;

                    byte currentTable = 0;
                    string databaseLine;

                    // Skip first four bytes (file header)
                    while (odbBytePosition < 5)
                    {
                        reader.ReadByte();
                        odbBytePosition++;
                    }

                    // Initialize first output file
                    if (csvFileNames.Length > 0)
                    {
                        outputStream = new StreamWriter(
                            Path.Combine(outputDirectory, csvFileNames[0]),
                            false,
                            Encoding.ASCII);
                    }

                    while (odbBytePosition < odbFileSize)
                    {
                        currentTable = reader.ReadByte();

                        // Check for table transition
                        if (odbTable != currentTable)
                        {
                            odbTable = currentTable;

                            // Close current file and open new one
                            outputStream?.Close();

                            if (odbTable < csvFileNames.Length && !string.IsNullOrEmpty(csvFileNames[odbTable]))
                            {
                                outputStream = new StreamWriter(
                                    Path.Combine(outputDirectory, csvFileNames[odbTable]),
                                    false,
                                    Encoding.ASCII);
                            }
                            else
                            {
                                // Use generic name for unknown tables
                                outputStream = new StreamWriter(
                                    Path.Combine(outputDirectory, $"Table_{odbTable}.csv"),
                                    false,
                                    Encoding.ASCII);
                            }
                        }

                        // Read line length
                        int stringLength = reader.ReadByte() + (reader.ReadByte() * 256);
                        odbBytePosition += 3;

                        // Read line content
                        databaseLine = string.Empty;
                        for (int i = 0; i < stringLength; i++)
                        {
                            databaseLine += reader.ReadChar();
                            odbBytePosition++;
                        }

                        // Convert tabs to commas and write
                        var formattedLine = databaseLine.Replace("\t", ",");
                        outputStream?.WriteLine(formattedLine);
                    }
                }
            }
            finally
            {
                outputStream?.Close();
            }
        }

        private async Task<IEnumerable<Dictionary<string, string>>> ReadCsvAsDictionariesAsync(string csvPath, int offset, int limit)
        {
            var result = new List<Dictionary<string, string>>();

            using var reader = new StreamReader(csvPath, Encoding.ASCII);

            // Read header
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
            {
                return result;
            }

            var headers = ParseCsvLine(headerLine).ToArray();

            // Skip offset rows
            for (int i = 0; i < offset; i++)
            {
                if (reader.EndOfStream)
                {
                    return result;
                }
                await reader.ReadLineAsync();
            }

            // Read up to limit rows
            int rowsRead = 0;
            while (!reader.EndOfStream && rowsRead < limit)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var values = ParseCsvLine(line).ToArray();
                var row = new Dictionary<string, string>();

                for (int i = 0; i < headers.Length && i < values.Length; i++)
                {
                    row[headers[i]] = values[i];
                }

                result.Add(row);
                rowsRead++;
            }

            return result;
        }

        private IEnumerable<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Add the last field
            result.Add(currentField.ToString().Trim());

            return result;
        }
        #endregion
    }
}