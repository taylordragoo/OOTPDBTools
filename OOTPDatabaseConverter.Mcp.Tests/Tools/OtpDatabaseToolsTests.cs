using FluentAssertions;
using Moq;
using OOTPDatabaseConverter.Core;
using OOTPDatabaseConverter.Mcp.Services;
using OOTPDatabaseConverter.Mcp.Tools;
using System.Text.Json;
using Xunit;

namespace OOTPDatabaseConverter.Mcp.Tests.Tools;

/// <summary>
/// Unit tests for OtpDatabaseTools.
/// </summary>
public class OtpDatabaseToolsTests
{
    private readonly Mock<IOtpDataProvider> _dataProviderMock;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OtpDatabaseToolsTests()
    {
        _dataProviderMock = new Mock<IOtpDataProvider>();
    }

    #region odb_load_database Tests

    [Fact]
    public async Task odb_load_database_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        var databasePath = "/valid/path";
        _dataProviderMock
            .Setup(x => x.LoadDatabaseAsync(databasePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _dataProviderMock
            .Setup(x => x.DetectVersionAsync())
            .ReturnsAsync(OdbVersion.ODB_26);

        // Act
        var result = await OtpDatabaseTools.odb_load_database(_dataProviderMock.Object, databasePath);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("message").GetString().Should().Contain("loaded successfully");
    }

    [Fact]
    public async Task odb_load_database_WithEmptyPath_ReturnsFailure()
    {
        // Act
        var result = await OtpDatabaseTools.odb_load_database(_dataProviderMock.Object, "");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("required");
    }

    [Fact]
    public async Task odb_load_database_WithNullPath_ReturnsFailure()
    {
        // Act
        var result = await OtpDatabaseTools.odb_load_database(_dataProviderMock.Object, null!);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("required");
    }

    [Fact]
    public async Task odb_load_database_WhenProviderThrows_ReturnsFailure()
    {
        // Arrange
        var databasePath = "/invalid/path";
        _dataProviderMock
            .Setup(x => x.LoadDatabaseAsync(databasePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Directory does not exist"));

        // Act
        var result = await OtpDatabaseTools.odb_load_database(_dataProviderMock.Object, databasePath);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("does not exist");
    }

    #endregion

    #region odb_list_tables Tests

    [Fact]
    public async Task odb_list_tables_WhenDatabaseNotLoaded_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(false);

        // Act
        var result = await OtpDatabaseTools.odb_list_tables(_dataProviderMock.Object);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("No database loaded");
    }

    [Fact]
    public async Task odb_list_tables_WhenDatabaseLoaded_ReturnsTables()
    {
        // Arrange
        var tables = new List<TableInfo>
        {
            new TableInfo { Name = "Master", DisplayName = "Players", RowCount = 100, SourceFile = "historical_database.odb" },
            new TableInfo { Name = "Batting", DisplayName = "Batting Stats", RowCount = 500, SourceFile = "historical_database.odb" }
        };

        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);
        _dataProviderMock
            .Setup(x => x.ListTablesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tables);

        // Act
        var result = await OtpDatabaseTools.odb_list_tables(_dataProviderMock.Object);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("totalTables").GetInt32().Should().Be(2);
    }

    #endregion

    #region odb_get_schema Tests

    [Fact]
    public async Task odb_get_schema_WhenDatabaseNotLoaded_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(false);

        // Act
        var result = await OtpDatabaseTools.odb_get_schema(_dataProviderMock.Object, "Master");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("No database loaded");
    }

    [Fact]
    public async Task odb_get_schema_WithEmptyTableName_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);

        // Act
        var result = await OtpDatabaseTools.odb_get_schema(_dataProviderMock.Object, "");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("required");
    }

    [Fact]
    public async Task odb_get_schema_WhenTableNotFound_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);
        _dataProviderMock
            .Setup(x => x.GetSchemaAsync("NonExistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TableSchema?)null);

        // Act
        var result = await OtpDatabaseTools.odb_get_schema(_dataProviderMock.Object, "NonExistent");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task odb_get_schema_WhenTableExists_ReturnsSchema()
    {
        // Arrange
        var schema = new TableSchema
        {
            TableName = "Master",
            Columns = new List<ColumnInfo>
            {
                new ColumnInfo { Name = "playerID", Index = 0, DataType = "string" },
                new ColumnInfo { Name = "nameFirst", Index = 1, DataType = "string" },
                new ColumnInfo { Name = "nameLast", Index = 2, DataType = "string" }
            }
        };

        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);
        _dataProviderMock
            .Setup(x => x.GetSchemaAsync("Master", It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act
        var result = await OtpDatabaseTools.odb_get_schema(_dataProviderMock.Object, "Master");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("tableName").GetString().Should().Be("Master");
        json.RootElement.GetProperty("columnCount").GetInt32().Should().Be(3);
    }

    #endregion

    #region odb_read_table Tests

    [Fact]
    public async Task odb_read_table_WhenDatabaseNotLoaded_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(false);

        // Act
        var result = await OtpDatabaseTools.odb_read_table(_dataProviderMock.Object, "Master");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("No database loaded");
    }

    [Fact]
    public async Task odb_read_table_WithEmptyTableName_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);

        // Act
        var result = await OtpDatabaseTools.odb_read_table(_dataProviderMock.Object, "");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("required");
    }

    [Fact]
    public async Task odb_read_table_WithNegativeOffset_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);

        // Act
        var result = await OtpDatabaseTools.odb_read_table(_dataProviderMock.Object, "Master", offset: -1);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("zero or greater");
    }

    [Fact]
    public async Task odb_read_table_WithValidParameters_ReturnsData()
    {
        // Arrange
        var pagedResult = new PagedResult<TableRow>
        {
            Items = new List<TableRow>
            {
                new TableRow { RowIndex = 0, Values = new[] { "player1", "John", "Doe" } },
                new TableRow { RowIndex = 1, Values = new[] { "player2", "Jane", "Smith" } }
            },
            Offset = 0,
            Limit = 100,
            TotalCount = 2
        };

        var schema = new TableSchema
        {
            TableName = "Master",
            Columns = new List<ColumnInfo>
            {
                new ColumnInfo { Name = "playerID", Index = 0 },
                new ColumnInfo { Name = "nameFirst", Index = 1 },
                new ColumnInfo { Name = "nameLast", Index = 2 }
            }
        };

        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);
        _dataProviderMock
            .Setup(x => x.ReadTableAsync("Master", 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);
        _dataProviderMock
            .Setup(x => x.GetSchemaAsync("Master", It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);

        // Act
        var result = await OtpDatabaseTools.odb_read_table(_dataProviderMock.Object, "Master");
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("rowCount").GetInt32().Should().Be(2);
    }

    #endregion

    #region odb_detect_version Tests

    [Fact]
    public async Task odb_detect_version_WhenDatabaseNotLoaded_ReturnsFailure()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(false);

        // Act
        var result = await OtpDatabaseTools.odb_detect_version(_dataProviderMock.Object);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("No database loaded");
    }

    [Fact]
    public async Task odb_detect_version_WhenDatabaseLoaded_ReturnsVersion()
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);
        _dataProviderMock
            .Setup(x => x.DetectVersionAsync())
            .ReturnsAsync(OdbVersion.ODB_26);

        // Act
        var result = await OtpDatabaseTools.odb_detect_version(_dataProviderMock.Object);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("version").GetString().Should().Be("ODB_26");
    }

    [Theory]
    [InlineData(OdbVersion.ODB_Err, "Error detecting version")]
    [InlineData(OdbVersion.ODB_17, "OOTP 17 or 18")]
    [InlineData(OdbVersion.ODB_19, "OOTP 19, 20, or 21")]
    [InlineData(OdbVersion.ODB_22, "OOTP 22, 23, or 24")]
    [InlineData(OdbVersion.ODB_25, "OOTP 25")]
    [InlineData(OdbVersion.ODB_26, "OOTP 26")]
    [InlineData(OdbVersion.ODB_Unk, "Unknown version")]
    public async Task odb_detect_version_ReturnsCorrectDescription(OdbVersion version, string expectedDescriptionPart)
    {
        // Arrange
        _dataProviderMock.Setup(x => x.IsLoaded).Returns(true);
        _dataProviderMock
            .Setup(x => x.DetectVersionAsync())
            .ReturnsAsync(version);

        // Act
        var result = await OtpDatabaseTools.odb_detect_version(_dataProviderMock.Object);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("description").GetString().Should().Contain(expectedDescriptionPart);
    }

    #endregion

    #region odb_unload_database Tests

    [Fact]
    public void odb_unload_database_CallsProvider()
    {
        // Arrange - mock is set up to track calls
        _dataProviderMock.Setup(x => x.UnloadDatabase());

        // Act
        var result = OtpDatabaseTools.odb_unload_database(_dataProviderMock.Object);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        _dataProviderMock.Verify(x => x.UnloadDatabase(), Times.Once);
    }

    #endregion
}