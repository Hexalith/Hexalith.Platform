using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(Hexalith.Platform.Works.Runtime.WorksRecoveryHostingStartup))]

namespace Hexalith.Platform.Works.Runtime;

/// <summary>Platform-injected HTTP defaults and telemetry registration; production operator authority remains unconfigured.</summary>
public sealed class WorksRecoveryHostingStartup : IHostingStartup
{
    /// <inheritdoc/>
    public void Configure(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Environment.GetEnvironmentVariable("WORKS_PLATFORM_RUNTIME") != "1")
            throw new InvalidOperationException("Platform Works hosting startup requires explicit host enablement.");
        builder.ConfigureServices((_, services) => services.AddWorksPlatformHosting());
    }
}
