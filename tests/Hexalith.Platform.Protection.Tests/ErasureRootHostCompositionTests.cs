using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Configuration admission happens before any service graph or native operation can fall back to an unprotected provider.</summary>
public sealed class ErasureRootHostCompositionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("true")]
    public void MissingOrDisabledInstallationRefusesStartupBeforeChangingServices(string? enabled)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IEventPayloadProtectionService>());
        var original = services.ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["EventStore:PayloadProtection:Enabled"] = enabled }).Build();
        var failure = Should.Throw<InvalidOperationException>(() => services.AddPlatformErasureRootProtection(configuration));
        failure.Message.ShouldStartWith("DependencyNotAvailable:EXT-PROTECTION-1:");
        services.ToArray().ShouldBe(original);
    }

    [Fact]
    public void UnpinnedInstallationCannotActivateDefaultProtection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:PayloadProtection:Enabled"] = "true",
            ["EventStore:PayloadProtection:InstallationFile"] = "/missing/protection-installation.json",
            ["EventStore:PayloadProtection:InstallationSha256"] = new string('0', 64),
        }).Build();
        Should.Throw<InvalidOperationException>(() => services.AddPlatformErasureRootProtection(configuration));
        services.ShouldBeEmpty();
    }
}
