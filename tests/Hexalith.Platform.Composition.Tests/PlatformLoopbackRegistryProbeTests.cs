namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Proves the qualification transport cannot extend plaintext inspection beyond canonical local Development registries.</summary>
public sealed class PlatformLoopbackRegistryProbeTests
{
    [Theory]
    [InlineData("localhost:15033/tenants:version", true)]
    [InlineData("127.0.0.1:15033/tenants:version", true)]
    [InlineData("[::1]:15033/tenants:version", true)]
    [InlineData("registry.hexalith.com/tenants:version", false)]
    [InlineData("localhost.example:15033/tenants:version", false)]
    [InlineData("user@localhost:15033/tenants:version", false)]
    [InlineData("2130706433:15033/tenants:version", false)]
    [InlineData("127.1:15033/tenants:version", false)]
    [InlineData("localhost:015033/tenants:version", false)]
    [InlineData("localhost/tenants:version", false)]
    [InlineData("localhost:15033?remote/tenants:version", false)]
    [InlineData("localhost:15033#remote/tenants:version", false)]
    public void QualificationRegistryRequiresAnUnambiguousLocalAuthority(string image, bool plaintext)
    {
        var arguments = PlatformLoopbackRegistryProbe.Arguments(image, WorkspaceMode.Package, development: true);
        arguments.Contains("--insecure").ShouldBe(plaintext);
        arguments.Last().ShouldBe(image);
    }

    [Theory]
    [InlineData(false, WorkspaceMode.Package)]
    [InlineData(true, WorkspaceMode.Source)]
    [InlineData(false, WorkspaceMode.Source)]
    public void EveryOtherEnvironmentAndModeRetainsTls(bool development, WorkspaceMode mode)
        => PlatformLoopbackRegistryProbe.Arguments("localhost:15033/tenants:version", mode, development)
            .ShouldBe(["manifest", "inspect", "localhost:15033/tenants:version"]);
}
