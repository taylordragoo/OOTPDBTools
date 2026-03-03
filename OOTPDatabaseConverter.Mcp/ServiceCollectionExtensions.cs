using Microsoft.Extensions.DependencyInjection;
using OOTPDatabaseConverter.Mcp.Services;

namespace OOTPDatabaseConverter.Mcp;

/// <summary>
/// Extension methods for configuring MCP server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the OOTP database services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOtpDatabaseServices(this IServiceCollection services)
    {
        // Register the data provider
        // TODO: Replace StubOtpDataProvider with the actual implementation
        services.AddSingleton<IOtpDataProvider, StubOtpDataProvider>();

        return services;
    }

    /// <summary>
    /// Adds the OOTP database services with a custom data provider implementation.
    /// </summary>
    /// <typeparam name="TDataProvider">The type of the data provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOtpDatabaseServices<TDataProvider>(this IServiceCollection services)
        where TDataProvider : class, IOtpDataProvider
    {
        services.AddSingleton<IOtpDataProvider, TDataProvider>();
        return services;
    }
}