using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Works.Tests;

/// <summary>Explicit environment for qualification-authority admission tests.</summary>
internal sealed class HostingEnvironment : IHostEnvironment
{
    /// <inheritdoc/>
    public string EnvironmentName { get; set; } = Environments.Production;

    /// <inheritdoc/>
    public string ApplicationName { get; set; } = "PlatformQualification";

    /// <inheritdoc/>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc/>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
