# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OOTP Database Converter is a .NET tool for converting Out of the Park Baseball (OOTP) database files between ODB and CSV formats. It supports OOTP versions 17 through 26. The project consists of four components:

- **OOTPDatabaseConverter.Core** - Shared conversion logic library
- **OOTPDatabaseConverter.Console** - Cross-platform console application
- **OOTPDatabaseConverter.Gui** - Cross-platform Avalonia UI application
- **OOTPDatabaseConverter.Mcp** - MCP (Model Context Protocol) server for AI integration

## Build Commands

```bash
# Build entire solution
dotnet build

# Build individual projects
dotnet build OOTPDatabaseConverter.Core
dotnet build OOTPDatabaseConverter.Console
dotnet build OOTPDatabaseConverter.Gui
dotnet build OOTPDatabaseConverter.Mcp
```

## Run Commands

```bash
# Run GUI (recommended)
./run-gui.sh
# Or directly:
dotnet run --project OOTPDatabaseConverter.Gui

# Run console app (interactive mode)
dotnet run --project OOTPDatabaseConverter.Console

# Run console app (command-line mode)
dotnet run --project OOTPDatabaseConverter.Console -- odb2csv /path/to/data
dotnet run --project OOTPDatabaseConverter.Console -- csv2odb /path/to/csv /output/dir

# Run MCP server
dotnet run --project OOTPDatabaseConverter.Mcp -- --help
dotnet run --project OOTPDatabaseConverter.Mcp -- --database /path/to/ootp/data
```

## Test Commands

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test OOTPDatabaseConverter.Mcp.Tests

# Run specific test class
dotnet test OOTPDatabaseConverter.Mcp.Tests --filter "FullyQualifiedName~OtpDatabaseToolsTests"
```

## Deployment

```bash
# Create portable self-contained executables
./build-portable.sh  # Linux/macOS
build-portable.bat   # Windows
```

## Architecture

### Core Library (OOTPDatabaseConverter.Core)

The conversion logic centers around several key classes:

- **OdbToCsv** - Main entry point for ODB→CSV conversion. Handles file verification, version detection, and orchestrates the conversion process.
- **CsvToOdb** - Handles CSV→ODB conversion, reconstructing database files from CSV data.
- **HistoricalDatabaseConverter** - Low-level binary file parser that reads ODB format and writes CSV.
- **FileNames** - Manages CSV file naming conventions based on OOTP version.
- **OdbVersion** - Enum representing supported OOTP database versions (17, 19, 22, 25, 26, Unknown).

### Version Detection

OOTP database format changes between versions. The `OdbToCsv.GetDatabaseVersion()` method detects the version by:
1. Reading table structure (number of tables varies by version)
2. Checking specific column headers (e.g., OOTP 22+ has "Glf" column in FieldingOF table)

### Progress Reporting

Both converters support `IProgress<Utilities.ProgressInfo>` for progress reporting with percentage and current file name. The GUI and console apps use this for real-time status updates.

### Data Files

The converter expects these ODB files in the input directory:
- `historical_database.odb` - Major league historical data
- `historical_minor_database.odb` - Minor league historical data
- `historical_lineups.odb` - Historical lineup data
- `historical_transactions.odb` - Historical transaction data

Output includes individual CSV files per table plus a `DatabaseConfig.txt` file used for reconstruction.

### GUI Architecture (OOTPDatabaseConverter.Gui)

- Uses Avalonia UI 11.x with MVVM pattern
- `MainWindowViewModel` uses CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- File dialogs use Avalonia's `IStorageProvider` API via `TopLevel.GetTopLevel()`

## Platform Notes

- Windows builds use `Encoding.Latin1`; other platforms use `Encoding.ASCII`
- Steam integration searches standard Steam installation paths for OOTP 26 data
- The `copy-ootp-data.sh` script copies ODB files from Steam to `./test-data` for testing

## MCP Server Architecture (OOTPDatabaseConverter.Mcp)

The MCP server exposes OOTP database access to AI assistants via the Model Context Protocol.

### Key Components

- **OtpDatabaseTools** - Static class with MCP tool methods (`[McpServerTool]` attributes)
- **IOtpDataProvider** - Stateful provider interface for the MCP layer
- **OotpDataProvider** - Adapter that bridges MCP layer to Core service
- **IOotpDataService** / **OotpDataService** - Stateless Core service for ODB file access

### MCP Tools Provided

| Tool | Description |
|------|-------------|
| `odb_load_database` | Load an OOTP database from a directory |
| `odb_list_tables` | List available tables |
| `odb_get_schema` | Get column headers for a table |
| `odb_read_table` | Read table data with pagination |
| `odb_detect_version` | Detect OOTP version (17-26) |
| `odb_unload_database` | Release resources |

### Data Flow

```
AI Client → MCP Protocol → OtpDatabaseTools → IOtpDataProvider → IOotpDataService → OdbToCsv/HistoricalDatabaseConverter
```

### MCP Client Configuration

```json
{
  "mcpServers": {
    "ootp-db": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/OOTPDatabaseConverter.Mcp", "--", "--database", "/path/to/ootp/data"]
    }
  }
}
```