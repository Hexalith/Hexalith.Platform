namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;
using Shouldly;
using Xunit;

/// <summary>Ensures isolated transport is applied only to the selected application's generated executable.</summary>
public sealed class DaprSidecarOwnershipTests
{
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
