#if HEXALITH_PLATFORM_CANONICAL_SCOPES
namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    /// <summary>A composition without the generated realm relays the scheduled Tenants task with runner-key credentials.</summary>
    [Fact]
    public async Task ScheduledTenantsTaskComposesTheRunnerKeyRelayAndAdmitsOnlyItsBoundDomainsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "operator-task-relay-" + Guid.NewGuid().ToString("N"));
        try
        {
            string partiesRoot = PlatformTestRepository.ReferenceRoot(Root, "Hexalith.Parties");
            var options = Options(active: "parties") with
            {
                DeclarationPaths = [Path.Combine(partiesRoot, "module", "hexalith.module-manifest.v2.json")],
            };
            var mapping = options.Mapping with { Entries = [.. options.Mapping.Entries,
                new SourceMappingEntry("Hexalith.Parties", "source", partiesRoot), new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            var plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace, CompositionPortAllocator.Allocate(1), [], [], mapping,
                options.DeclarationPaths, "parties", typeof(PlatformAppModel).Assembly.Location);
            string invocation = CompositionStartupTasks.InvocationPathFor(workspace, "tenants", "create-synthetic-tenant");
            plan = plan with
            {
                StartupTasks = [new CompositionStartupTask("tenants", "create-synthetic-tenant", "/unused", [])
                {
                    Scope = CompositionStartupTasks.EnvironmentCreationScope,
                    InvocationPath = invocation,
                }],
            };
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            var builder = Builder();
            PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), Path.Combine(PlatformTestRepository.ReferenceRoot(Root, "Hexalith.Builds"), "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj"));

            IResource gateway = builder.Resources.Single(resource => resource.Name == "eventstore");
            Dictionary<string, object> environment = [];
            foreach (var callback in gateway.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway, environment, TestContext.Current.CancellationToken));
            // Only the bootstrap and synthetic-tenant domains are admitted on the bound Tenants channel.
            environment["Authentication__BoundCommand__AllowedDomains__tenants"].ShouldBe("global-administrators,tenants");

            await using var provider = builder.Services.BuildServiceProvider();
            provider.GetServices<ICompositionRunPreparation>().OfType<PlatformOperatorTaskRelay>().ShouldHaveSingleItem();
            // Without the generated realm the cutover keeps the runner signing key.
            provider.GetServices<ICompositionOperatorTokenSource>().ShouldBeEmpty();
            // The invocation is written by the started relay, before readiness, never at composition time.
            File.Exists(invocation).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
        }
    }
}
#endif
