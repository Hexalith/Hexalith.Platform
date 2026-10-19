using System.Reflection;

using Hexalith.Platform.Works.Runtime;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Hexalith.Platform.Works.Tests;

/// <summary>Both Platform runtime entry points refuse without explicit host enablement and register telemetry once with it.</summary>
public sealed class PlatformRuntimeEntryPointTests
{
    private const string Gate = "WORKS_PLATFORM_RUNTIME";

    /// <summary>
    /// The startup hook and the hosting startup each refuse every value other than <c>1</c> before installing anything;
    /// enabled, a repeated hosting startup and the parity proof's own call still register one telemetry publisher.
    /// </summary>
    /// <remarks>Only these two entry points read the gate, and both cases run in this one test, so no parallel test observes it.</remarks>
    [Fact]
    public void BothEntryPointsRefuseWithoutExplicitEnablementAndRegisterTelemetryOnceWithIt()
    {
        string? original = Environment.GetEnvironmentVariable(Gate);
        MethodInfo hook = typeof(WorksRecoveryHostingStartup).Assembly.GetType("StartupHook", throwOnError: true)!
            .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();
        try
        {
            foreach (string? value in new[] { null, string.Empty, "0", "true", " 1" })
            {
                Environment.SetEnvironmentVariable(Gate, value);
                Should.Throw<TargetInvocationException>(() => hook.Invoke(null, null)).InnerException
                    .ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("explicit host enablement");
                var refused = new CapturingWebHostBuilder();
                Should.Throw<InvalidOperationException>(() => new WorksRecoveryHostingStartup().Configure(refused))
                    .Message.ShouldContain("explicit host enablement");
                refused.Services.ShouldBeEmpty();
            }

            Environment.SetEnvironmentVariable(Gate, "1");
            Should.NotThrow(() => hook.Invoke(null, null));
            var enabled = new CapturingWebHostBuilder();
            new WorksRecoveryHostingStartup().Configure(enabled);
            new WorksRecoveryHostingStartup().Configure(enabled);
            enabled.Services.AddWorksRecoveryTelemetry();
            enabled.Services.Count(descriptor => descriptor.ServiceType == typeof(WorksRecoveryTelemetryPublisher)).ShouldBe(1);
            enabled.Services.Count(descriptor => descriptor.ServiceType == typeof(IHealthCheckPublisher)).ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Gate, original);
        }
    }
}
