namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    /// <summary>Each declared receiver preserves the SDK's actual channel authentication boundary.</summary>
    [Theory]
    [InlineData("domain")]
    [InlineData("api")]
    public void DeclaredTenantsChannelTokenRetainsSdkAuthenticationBoundary(string server)
    {
        string ownToken = CreateChannelToken();
        string otherToken = CreateChannelToken();
        string daprToken = CreateChannelToken();
        PlatformCompositionOptions options = Options(active: "tenants") with
        {
            Configuration = new Dictionary<string, string>
            {
                ["tenants/" + server + "/Tenants:AppChannelToken"] = ownToken,
                ["tenants/" + server + "/Tenants:ApiDaprToken"] = daprToken,
            },
        };
        PlatformModuleDeclaration module = PlatformDeclarationSet.Load(options).Single(value => PlatformDeclarationSet.Id(value.Declaration) == "tenants");
        Dictionary<string, string> environment = PlatformLocalProfile.Bind(options, module, server);
        string expected = environment["APP_API_TOKEN"];
        expected.ShouldBe(ownToken);
        var request = new DefaultHttpContext().Request;
        DaprAppChannelToken.Verify(request, expected, true).ShouldBe(DaprAppChannelTokenStatus.Missing);
        request.Headers[DaprAppChannelToken.HeaderName] = otherToken;
        DaprAppChannelToken.Verify(request, expected, true).ShouldBe(DaprAppChannelTokenStatus.Invalid);
        request.Headers[DaprAppChannelToken.HeaderName] = daprToken;
        DaprAppChannelToken.Verify(request, expected, true).ShouldBe(DaprAppChannelTokenStatus.Invalid);
        request.Headers[DaprAppChannelToken.HeaderName] = ownToken;
        DaprAppChannelToken.Verify(request, expected, true).ShouldBe(DaprAppChannelTokenStatus.Valid);
        request.Headers[DaprAppChannelToken.HeaderName] = new[] { ownToken, ownToken };
        DaprAppChannelToken.Verify(request, expected, true).ShouldBe(DaprAppChannelTokenStatus.Duplicate);
        DaprAppChannelToken.Verify(request, null, true).ShouldBe(DaprAppChannelTokenStatus.Unconfigured);
    }

    /// <summary>An otherwise fully supplied unknown role cannot acquire either declared runtime credential alias.</summary>
    [Fact]
    public void UnknownTenantsServerDoesNotAcquireDeclaredChannelAliases()
    {
        var defaults = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations", "local-profile.json")))!;
        PlatformCompositionOptions options = Options(active: "tenants") with
        {
            Configuration = defaults.Where(pair => pair.Key.StartsWith("tenants/domain/", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key.Replace("tenants/domain/", "tenants/worker/", StringComparison.Ordinal), pair => pair.Value),
        };
        PlatformModuleDeclaration module = PlatformDeclarationSet.Load(options).Single(value => PlatformDeclarationSet.Id(value.Declaration) == "tenants");
        Dictionary<string, string> environment = PlatformLocalProfile.Bind(options, module, "worker", new Dictionary<string, string>(StringComparer.Ordinal));
        environment["Tenants__AppChannelToken"].ShouldNotBeNullOrEmpty();
        environment.ShouldNotContainKey("APP_API_TOKEN");
        environment.ShouldNotContainKey("DAPR_API_TOKEN");
    }

    /// <summary>Creates a runtime-only fixture credential without embedding reusable secret material in source.</summary>
    private static string CreateChannelToken() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}
