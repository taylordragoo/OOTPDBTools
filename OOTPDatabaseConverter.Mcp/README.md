# OOTP Database MCP Server

An MCP (Model Context Protocol) server that provides tools for querying OOTP (Out of the Park Baseball) database files.

## Overview

This project implements an MCP server that allows AI assistants and other MCP clients to query OOTP database files. It provides read-only access to the data stored in OOTP's proprietary `.odb` format.

## Features

- **Load Database**: Load OOTP database files from a directory
- **List Tables**: Enumerate all tables available in the database
- **Get Schema**: Retrieve column headers and types for any table
- **Read Table Data**: Query table data with pagination support
- **Detect Version**: Automatically detect the OOTP version of the database

## Installation

Build the project:
```bash
dotnet build
```

## Usage

### Running the Server

```bash
# Run with help
dotnet run --project OOTPDatabaseConverter.Mcp -- --help

# Run with a pre-configured database path
dotnet run --project OOTPDatabaseConverter.Mcp -- --database /path/to/ootp/database

# Run with environment variable for logging
MCP_LOG_LEVEL=Debug dotnet run --project OOTPDatabaseConverter.Mcp
```

### MCP Client Configuration

Add to your MCP client configuration (e.g., Claude Desktop):

```json
{
  "mcpServers": {
    "ootp-db": {
      "command": "/path/to/OOTPDatabaseConverter.Mcp",
      "args": ["--database", "/path/to/ootp/database"]
    }
  }
}
```

## MCP Tools

### odb_load_database

Load an OOTP database from a directory path.

**Parameters:**
- `databasePath` (string, required): Path to the directory containing OOTP database files

**Example:**
```json
{
  "name": "odb_load_database",
  "arguments": {
    "databasePath": "/path/to/ootp/database"
  }
}
```

### odb_list_tables

List all tables available in the loaded database.

**Parameters:** None

**Example:**
```json
{
  "name": "odb_list_tables",
  "arguments": {}
}
```

### odb_get_schema

Get the schema (column headers) for a specific table.

**Parameters:**
- `tableName` (string, required): Name of the table to get the schema for

**Example:**
```json
{
  "name": "odb_get_schema",
  "arguments": {
    "tableName": "Master"
  }
}
```

### odb_read_table

Read data from a table with pagination support.

**Parameters:**
- `tableName` (string, required): Name of the table to read
- `offset` (int, optional): Zero-based offset for pagination (default: 0)
- `limit` (int, optional): Maximum rows to return (default: 100, max: 1000)

**Example:**
```json
{
  "name": "odb_read_table",
  "arguments": {
    "tableName": "Batting",
    "offset": 0,
    "limit": 50
  }
}
```

### odb_detect_version

Detect the OOTP version of the loaded database.

**Parameters:** None

**Example:**
```json
{
  "name": "odb_detect_version",
  "arguments": {}
}
```

## Supported OOTP Versions

- OOTP 17/18 (ODB_17)
- OOTP 19/20/21 (ODB_19)
- OOTP 22/23/24 (ODB_22)
- OOTP 25 (ODB_25)
- OOTP 26+ (ODB_26)

## Architecture

### Project Structure

```
OOTPDatabaseConverter.Mcp/
├── Program.cs                    # Main entry point and host configuration
├── ServiceCollectionExtensions.cs # DI configuration helpers
├── Services/
│   ├── IOtpDataProvider.cs      # Service interface contract
│   └── StubOtpDataProvider.cs   # Stub implementation for testing
└── Tools/
    └── OtpDatabaseTools.cs      # MCP tool implementations
```

### Dependency Injection

The server uses dependency injection for all components:

- `IOtpDataProvider` - Service interface for database operations
- `OtpDatabaseTools` - MCP tools that use the data provider

### Interface Contract

The `IOtpDataProvider` interface defines the contract that data access implementations must follow:

```csharp
public interface IOtpDataProvider
{
    string? DatabasePath { get; }
    bool IsLoaded { get; }
    Task<bool> LoadDatabaseAsync(string databasePath, CancellationToken cancellationToken = default);
    Task<OdbVersion> DetectVersionAsync();
    Task<IReadOnlyList<TableInfo>> ListTablesAsync(CancellationToken cancellationToken = default);
    Task<TableSchema?> GetSchemaAsync(string tableName, CancellationToken cancellationToken = default);
    Task<PagedResult<TableRow>> ReadTableAsync(string tableName, int offset, int limit, CancellationToken cancellationToken = default);
    void UnloadDatabase();
}
```

## Implementation Notes

### TODO: Data Access Implementation

The current implementation uses `StubOtpDataProvider` which returns placeholder data. The actual implementation should:

1. **LoadDatabaseAsync**:
   - Validate the directory exists
   - Check for required ODB files (`historical_database.odb`, `historical_minor_database.odb`, etc.)
   - Parse database structure using `OdbToCsv` or similar logic
   - Cache table metadata for quick access

2. **DetectVersionAsync**:
   - Use the `OdbToCsv.GetDatabaseVersion()` logic to detect OOTP version
   - Map to the appropriate `OdbVersion` enum value

3. **ListTablesAsync**:
   - Use the `FileNames` class to get table names based on detected version
   - Optionally count rows in each table

4. **GetSchemaAsync**:
   - Read the first row of the table to extract column headers
   - The ODB format stores column headers as the first row in each table

5. **ReadTableAsync**:
   - Parse the ODB file to extract rows
   - Apply pagination (offset, limit)
   - Return as `TableRow` objects

### ODB File Format

OOTP database files use a binary format:
- 4-byte header at the start
- Tables are indexed by a byte identifier
- Each row is prefixed with a 2-byte length indicator
- Tab-delimited values within rows

## Related Projects

- `OOTPDatabaseConverter.Core` - Core library for ODB file parsing
- `OOTPDatabaseConverter.Console` - Console application for batch conversion
- `OOTPDatabaseConverter.Gui` - GUI application for interactive conversion

## License

GNU General Public License v2.0 or later