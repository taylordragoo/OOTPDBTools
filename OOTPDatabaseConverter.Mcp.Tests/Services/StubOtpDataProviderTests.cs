using FluentAssertions;
using OOTPDatabaseConverter.Core;
using OOTPDatabaseConverter.Mcp.Services;
using Xunit;

namespace OOTPDatabaseConverter.Mcp.Tests.Services;

/// <summary>
/// Unit tests for StubOtpDataProvider.
/// </summary>
public class StubOtpDataProviderTests
{
    private readonly StubOtpDataProvider _provider;

    public StubOtpDataProviderTests()
    {
        _provider = new StubOtpDataProvider();
    }

    #region Initial State Tests

    [Fact]
    public void Constructor_InitializesWithNoDatabaseLoaded()
    {
        // Assert
        _provider.IsLoaded.Should().BeFalse();
        _provider.DatabasePath.Should().BeNull();
    }

    #endregion

    #region LoadDatabaseAsync Tests

    [Fact]
    public async Task LoadDatabaseAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.LoadDatabaseAsync(null!));
    }

    [Fact]
    public async Task LoadDatabaseAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.LoadDatabaseAsync(""));
    }

    [Fact]
    public async Task LoadDatabaseAsync_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.LoadDatabaseAsync("   "));
    }

    [Fact]
    public async Task LoadDatabaseAsync_WithNonexistentDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.LoadDatabaseAsync("/nonexistent/path/12345"));
    }

    #endregion

    #region DetectVersionAsync Tests

    [Fact]
    public async Task DetectVersionAsync_WhenNotLoaded_ReturnsOdbErr()
    {
        // Act
        var result = await _provider.DetectVersionAsync();

        // Assert
        result.Should().Be(OdbVersion.ODB_Err);
    }

    #endregion

    #region ListTablesAsync Tests

    [Fact]
    public async Task ListTablesAsync_WhenNotLoaded_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.ListTablesAsync());
    }

    #endregion

    #region GetSchemaAsync Tests

    [Fact]
    public async Task GetSchemaAsync_WhenNotLoaded_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.GetSchemaAsync("Master"));
    }

    [Fact]
    public async Task GetSchemaAsync_WithNullTableName_ThrowsArgumentException()
    {
        // Arrange - Load a database first
        // Note: Since this is a stub, we need to create a temporary directory
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.GetSchemaAsync(null!));
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task GetSchemaAsync_WithEmptyTableName_ThrowsArgumentException()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.GetSchemaAsync(""));
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task GetSchemaAsync_ForUnknownTable_ReturnsNull()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act
            var result = await _provider.GetSchemaAsync("UnknownTable");

            // Assert
            result.Should().BeNull();
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task GetSchemaAsync_ForMasterTable_ReturnsSchema()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act
            var result = await _provider.GetSchemaAsync("Master");

            // Assert
            result.Should().NotBeNull();
            result!.TableName.Should().Be("Master");
            result.Columns.Should().NotBeEmpty();
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    #endregion

    #region ReadTableAsync Tests

    [Fact]
    public async Task ReadTableAsync_WhenNotLoaded_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.ReadTableAsync("Master"));
    }

    [Fact]
    public async Task ReadTableAsync_WithNullTableName_ThrowsArgumentException()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.ReadTableAsync(null!));
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task ReadTableAsync_WithNegativeOffset_ThrowsArgumentException()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.ReadTableAsync("Master", offset: -1));
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task ReadTableAsync_WithZeroLimit_ThrowsArgumentException()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.ReadTableAsync("Master", limit: 0));
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task ReadTableAsync_WithNegativeLimit_ThrowsArgumentException()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.ReadTableAsync("Master", limit: -1));
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    [Fact]
    public async Task ReadTableAsync_ReturnsEmptyResult()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();

        try
        {
            await _provider.LoadDatabaseAsync(tempPath);

            // Act
            var result = await _provider.ReadTableAsync("Master", offset: 0, limit: 100);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
            result.HasMore.Should().BeFalse();
        }
        finally
        {
            _provider.UnloadDatabase();
        }
    }

    #endregion

    #region UnloadDatabase Tests

    [Fact]
    public void UnloadDatabase_ResetsState()
    {
        // Arrange - Load a database first
        var tempPath = Path.GetTempPath();
        _provider.LoadDatabaseAsync(tempPath).Wait();

        // Act
        _provider.UnloadDatabase();

        // Assert
        _provider.IsLoaded.Should().BeFalse();
        _provider.DatabasePath.Should().BeNull();
    }

    #endregion
}