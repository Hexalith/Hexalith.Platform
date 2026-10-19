using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Works.Tests;

/// <summary>Applies hosting-startup service registrations to an inspectable collection without building a host.</summary>
internal sealed class CapturingWebHostBuilder : IWebHostBuilder
{
    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the services every applied registration added.</summary>
    internal ServiceCollection Services { get; } = [];

    // IWebHostBuilder still requires this member; the double never builds a host.
#pragma warning disable ASPDEPR008
    /// <inheritdoc/>
    public IWebHost Build() => throw new NotSupportedException("The capturing builder never builds a host.");
#pragma warning restore ASPDEPR008

    /// <inheritdoc/>
    public IWebHostBuilder ConfigureAppConfiguration(Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate) => this;

    /// <inheritdoc/>
    public IWebHostBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        configureServices(Services);
        return this;
    }

    /// <inheritdoc/>
    public IWebHostBuilder ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        configureServices(new WebHostBuilderContext(), Services);
        return this;
    }

    /// <inheritdoc/>
    public string? GetSetting(string key) => _settings.GetValueOrDefault(key);

    /// <inheritdoc/>
    public IWebHostBuilder UseSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }
}
