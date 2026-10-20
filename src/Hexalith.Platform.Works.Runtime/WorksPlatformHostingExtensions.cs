using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>Platform-owned HTTP defaults for the SDK domain-service host.</summary>
public static class WorksPlatformHostingExtensions
{
    /// <summary>Registers RFC 9457 error handling before the domain pipeline and the existing recovery telemetry.</summary>
    public static IServiceCollection AddWorksPlatformHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProblemDetails();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, WorksProblemDetailsStartupFilter>());
        return services.AddWorksRecoveryTelemetry();
    }
}
