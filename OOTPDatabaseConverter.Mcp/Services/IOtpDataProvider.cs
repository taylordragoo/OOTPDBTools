using OOTPDatabaseConverter.Core;

namespace OOTPDatabaseConverter.Mcp.Services;

/// <summary>
/// Provides access to OOTP database table data.
/// This interface defines the contract for data access operations that will be
/// implemented by the data access layer.
/// </summary>
public interface IOtpDataProvider
{
    /// <summary>
    /// Gets the path to the currently loaded OOTP database.
    /// </summary>
    string? DatabasePath { get; }

    /// <summary>
    /// Gets a value indicating whether a database is currently loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Loads an OOTP database from the specified directory path.
    /// </summary>
    /// <param name="databasePath">Path to the directory containing OOTP database files.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if the database was loaded successfully.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the database cannot be loaded.</exception>
    Task<bool> LoadDatabaseAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the OOTP version of the currently loaded database.
    /// </summary>
    /// <returns>The detected OdbVersion, or OdbVersion.ODB_Err if no database is loaded.</returns>
    Task<OdbVersion> DetectVersionAsync();

    /// <summary>
    /// Gets a list of all available tables in the loaded database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A list of table information objects.</returns>
    Task<IReadOnlyList<TableInfo>> ListTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema (column headers) for a specific table.
    /// </summary>
    /// <param name="tableName">The name of the table to get the schema for.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The table schema, or null if the table doesn't exist.</returns>
    Task<TableSchema?> GetSchemaAsync(string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data from a table with pagination support.
    /// </summary>
    /// <param name="tableName">The name of the table to read.</param>
    /// <param name="offset">The zero-based offset for pagination.</param>
    /// <param name="limit">The maximum number of rows to return.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A paged result containing the table data.</returns>
    Task<PagedResult<TableRow>> ReadTableAsync(
        string tableName,
        int offset = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads the current database and releases resources.
    /// </summary>
    void UnloadDatabase();
}

/// <summary>
/// Represents information about a database table.
/// </summary>
public record TableInfo
{
    /// <summary>
    /// Gets or sets the name of the table.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the display name of the table (human-readable).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the number of rows in the table.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Gets or sets the database file this table belongs to.
    /// </summary>
    public string? SourceFile { get; init; }
}

/// <summary>
/// Represents the schema of a database table.
/// </summary>
public record TableSchema
{
    /// <summary>
    /// Gets or sets the name of the table.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Gets or sets the column definitions.
    /// </summary>
    public required IReadOnlyList<ColumnInfo> Columns { get; init; }
}

/// <summary>
/// Represents information about a table column.
/// </summary>
public record ColumnInfo
{
    /// <summary>
    /// Gets or sets the name of the column.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the zero-based index of the column.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or sets the data type of the column (if known).
    /// </summary>
    public string? DataType { get; init; }
}

/// <summary>
/// Represents a row of data from a table.
/// </summary>
public record TableRow
{
    /// <summary>
    /// Gets or sets the zero-based row index.
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// Gets or sets the column values as an array of strings.
    /// </summary>
    public required string[] Values { get; init; }
}

/// <summary>
/// Represents a paged result from a query.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public record PagedResult<T>
{
    /// <summary>
    /// Gets or sets the items in the current page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets or sets the zero-based offset of this page.
    /// </summary>
    public int Offset { get; init; }

    /// <summary>
    /// Gets or sets the number of items in this page.
    /// </summary>
    public int Limit { get; init; }

    /// <summary>
    /// Gets or sets the total number of items available.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether there are more items available.
    /// </summary>
    public bool HasMore => Offset + Items.Count < TotalCount;
}