// <copyright file="AppHostRunnerValidationTests.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Builds.ModuleTool.Tests.Workspace;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.Builds.ModuleHosts.AppHost;
using Hexalith.Builds.Tooling.Runtime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

public sealed class AppHostRunnerValidationTests
{
    /// <summary>Verifies healthy resources cannot publish readiness before the owning preparation completes.</summary>
    /// <returns>The actual readiness publication assertions.</returns>
    [Fact]
    public async Task HealthyResourcesWaitForOwnerPreparationBeforePublishingAsync()
    {
        string root = CompositionTestFiles.CreateDirectory();
        try
        {
            CompositionRunPlan plan = CompositionTestFiles.CreatePlan(root) with { Modules = [], UiMarkers = [] };
            IDistributedApplicationBuilder builder = CreateBuilder();
            ExecutableResource resource = builder.AddExecutable("eventstore", "unused", root).WithHttpEndpoint(port: 32124).Resource;
            ExecutableResource sidecar = builder.AddExecutable("eventstore-dapr-cli", "unused", root).Resource;
            EndpointAnnotation endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single();
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 32124);
            await using DistributedApplication app = builder.Build();
            ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
            await PublishAsync(notifications, resource, HealthStatus.Healthy, 4243).ConfigureAwait(true);
            await PublishAsync(notifications, sidecar, HealthStatus.Healthy, 4243).ConfigureAwait(true);
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(5));
            TaskCompletionSource preparation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> readiness = RunReadiness.WaitAndWriteAsync(app, plan, preparation.Task, bound.Token);
            Task observationWindow = Task.Delay(250, bound.Token);
            (await Task.WhenAny(readiness, observationWindow).ConfigureAwait(true)).ShouldBeSameAs(observationWindow);
            File.Exists(plan.ReadinessPath).ShouldBeFalse();
            preparation.SetResult();
            (await readiness.ConfigureAwait(true)).ShouldBeTrue();
            File.Exists(plan.ReadinessPath).ShouldBeTrue();
        }
        finally
        {
            CompositionTestFiles.Delete(root);
        }
    }
    private static IDistributedApplicationBuilder CreateBuilder() => DistributedApplication.CreateBuilder(new DistributedApplicationOptions
    {
        Args = [],
        AssemblyName = typeof(RunTopology).Assembly.GetName().Name,
        DisableDashboard = true,
        AllowUnsecuredTransport = true,
    });

    private static Task PublishAsync(ResourceNotificationService notifications, IResource resource, HealthStatus health, object? processId) =>
        notifications.PublishUpdateAsync(resource, snapshot =>
        {
            CustomResourceSnapshot observed = (snapshot with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Running, string.Empty),
                Properties = processId is null ? [] : [new ResourcePropertySnapshot("executable.pid", processId)],
            }).WithHealthReports([new HealthReportSnapshot("ready", health, "module-ready", string.Empty)]);
            if (health == HealthStatus.Healthy)
            {
                // Aspire also waits for its internal resource-ready event after the module health check succeeds.
                PropertyInfo ready = typeof(CustomResourceSnapshot).GetProperty("ResourceReadyEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
                ready.SetValue(observed, Activator.CreateInstance(ready.PropertyType, Task.CompletedTask));
            }

            return observed;
        });

}
