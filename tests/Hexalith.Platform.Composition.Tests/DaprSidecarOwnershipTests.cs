namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;
using Shouldly;
using Xunit;

/// <summary>Ensures isolated transport is applied only to the selected application's generated executable.</summary>
public sealed class DaprSidecarOwnershipTests
{
    /// <summary>Both control-plane clients obtain their identity from the selected private Sentry service.</summary>
    [Fact]
    public async Task ControlPlaneRoutesIdentityRequestsToSelectedSentry()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var sentry = builder.AddContainer("selected-sentry", "fixture");
        var workspace = Directory.CreateTempSubdirectory();
        try
        {
            var (placement, scheduler) = DaprSelfHostedMtls.AddControlPlane(builder, sentry,
                workspace.FullName, Path.Combine(workspace.FullName, "scheduler"), 32103, 32104);
            foreach (var peer in new[] { placement.Resource, scheduler.Resource })
            {
                Dictionary<string, object> environment = [];
                foreach (var callback in peer.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, peer, environment, TestContext.Current.CancellationToken));
                environment["NO_PROXY"].ShouldBe("localhost,127.0.0.1,::1,selected-sentry");
                environment["no_proxy"].ShouldBe(environment["NO_PROXY"]);

                List<object> arguments = [];
                foreach (var callback in peer.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                    await callback.Callback(new CommandLineArgsCallbackContext(arguments, peer, TestContext.Current.CancellationToken));
                arguments.ShouldContain("--sentry-address=selected-sentry:50001");
            }
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    /// <summary>Name collisions, missing parents and conflicting parents cannot receive the selected owner's transport.</summary>
    [Theory]
    [InlineData("project")]
    [InlineData("foreign")]
    [InlineData("unowned")]
    [InlineData("ambiguous")]
    public void InvalidSidecarRefusesBeforeAnyAnnotationIsChanged(string scenario)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var owner = builder.AddContainer("owner", "fixture");
        var foreign = builder.AddContainer("foreign", "fixture");
        const string name = "owner-dapr-cli";
        IResourceBuilder<ExecutableResource> sidecar = scenario == "project"
            ? builder.AddResource(new DotnetProjectResource(name, builder.AppHostDirectory))
            : builder.AddExecutable(name, "dapr", builder.AppHostDirectory);
        if (scenario is "foreign" or "ambiguous") sidecar.WithParentRelationship(foreign);
        if (scenario is "project" or "ambiguous")
            sidecar.WithAnnotation(new ResourceRelationshipAnnotation(owner.Resource, "Parent"));
        sidecar.WithHttpEndpoint(port: 32101, targetPort: 32102);
        var annotations = sidecar.Resource.Annotations.ToArray();
        var endpoint = annotations.OfType<EndpointAnnotation>().Single();
        var command = sidecar.Resource.Command;
        var model = new DistributedApplicationModel(builder.Resources);

        Should.Throw<InvalidDataException>(() => DaprSelfHostedMtls.BindGeneratedSidecar(model, owner.Resource, "private-credentials"))
            .Message.ShouldContain("must belong to the selected application");
        sidecar.Resource.Annotations.ToArray().ShouldBe(annotations);
        sidecar.Resource.Command.ShouldBe(command);
        endpoint.TargetPort.ShouldBe(32102);
        endpoint.IsProxied.ShouldBeTrue();
    }
}
