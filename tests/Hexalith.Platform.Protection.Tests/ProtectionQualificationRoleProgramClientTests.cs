using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>The shipped HTTPS role client verifies the endpoint before it presents a bearer, then correlates the signed service response.</summary>
public sealed class ProtectionQualificationRoleProgramClientTests
{
    [Fact]
    public async Task ShippedClientPresentsBearerOnlyAfterEndpointProofAndCorrelatesTheSignedResponse()
    {
        var token = TestContext.Current.CancellationToken;
        const string credentialName = "EXT_PROTECTION_ROLE_LOOP8_CLIENT";
        const string credential = "owned-component-test-bearer";
        const string binding = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string directory = Path.Combine(Path.GetTempPath(), "ext-protection-loop8-role-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var fixture = new ErasureRootEngineFixture();
        using var reader = Substitute.For<DaprClient>();
        Environment.SetEnvironmentVariable(credentialName, credential);
        WebApplication? app = null;
        try
        {
            string anchor = Path.Combine(directory, "public.pem");
            await File.WriteAllTextAsync(anchor, signer.ExportSubjectPublicKeyInfoPem(), token);
            string anchorSha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(anchor, token)));
            reader.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-test", deploymentIdentity = "owned-role" }));
            var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
                ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject(), StringComparer.Ordinal);
            var recipes = ProtectionQualificationSourceCatalog.Compile(arguments);
            var observations = recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()) { CaseId = id }, StringComparer.Ordinal);
            var scope = new ProtectionQualificationServiceScope("enrollment", "loop8-enrollment", "tenant-a", binding, new("installation", "proof", new()), recipes, observations);
            var service = new ProtectionQualificationRoleService(scope, new(fixture.Custody, fixture.Permissions), null, null, reader, signer);
            using var certificateKey = RSA.Create(2048);
            var request = new CertificateRequest("CN=127.0.0.1", certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder(); names.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(names.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            app = ProtectionQualificationRoleApplication.Build(service, "env:" + credentialName, configure: builder =>
                builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate))));
            await app.StartAsync(token);
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
            var installation = new ProtectionQualificationRoleInstallation("ext-protection-1-role-program/v1", "enrollment", "loop8-enrollment",
                address + "/platform-protection/v1/role", "env:" + credentialName, anchor, anchorSha);
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.Thumbprint == certificate.Thumbprint };
            var observation = new ProtectionQualificationClientObservation(handler);
            using var http = new HttpClient(observation) { Timeout = TimeSpan.FromSeconds(40) };
            var body = new JsonObject { ["operationId"] = "loop8-inspect", ["action"] = "InspectInstallation", ["bindingDigest"] = binding };
            byte[] input = ProtectionQualificationCanonical.Bytes(body);
            byte[] output = await ProtectionQualificationRoleProgram.InvokeAsync(http, installation, input, token);
            observation.Requests.Count.ShouldBe(2);
            observation.Requests[0].Authorization.ShouldBeNull();
            observation.Requests[1].Authorization.ShouldBe("Bearer " + credential);
            observation.Requests[1].Contract.ShouldBe("ext-protection-1-role-program/v1");
            var statement = JsonNode.Parse(output)!["statement"]!.AsObject();
            statement["operationId"]!.GetValue<string>().ShouldBe("loop8-inspect");
            statement["bindingDigest"]!.GetValue<string>().ShouldBe(binding);
            statement["requestSha256"]!.GetValue<string>().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(input)));
            statement["role"]!.GetValue<string>().ShouldBe("enrollment");
            statement["identity"]!.GetValue<string>().ShouldBe("loop8-enrollment");
        }
        finally
        {
            if (app is not null) { await app.DisposeAsync(); }
            Environment.SetEnvironmentVariable(credentialName, null);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SubstitutedEndpointReceivesNoBearer()
    {
        var token = TestContext.Current.CancellationToken;
        const string credentialName = "EXT_PROTECTION_ROLE_LOOP8_SUBSTITUTE";
        string directory = Path.Combine(Path.GetTempPath(), "ext-protection-loop8-substitute-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable(credentialName, "owned-component-test-bearer");
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string anchor = Path.Combine(directory, "public.pem");
            await File.WriteAllTextAsync(anchor, signer.ExportSubjectPublicKeyInfoPem(), token);
            var installation = new ProtectionQualificationRoleInstallation("ext-protection-1-role-program/v1", "enrollment", "loop8-enrollment",
                "https://substitute.invalid/role", "env:" + credentialName, anchor, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(anchor, token))));
            var observation = new ProtectionQualificationClientObservation(new HttpClientHandler()) { Refuse = true };
            using var http = new HttpClient(observation) { Timeout = TimeSpan.FromSeconds(5) };
            var body = ProtectionQualificationCanonical.Bytes(new JsonObject { ["operationId"] = "loop8-substitute", ["action"] = "InspectInstallation", ["bindingDigest"] = new string('a', 64) });
            await Should.ThrowAsync<InvalidOperationException>(() => ProtectionQualificationRoleProgram.InvokeAsync(http, installation, body, token));
            observation.Requests.Count.ShouldBe(1);
            observation.Requests[0].Authorization.ShouldBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(credentialName, null);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UnacceptedRoleConfigurationDoesNotOpenAConnection()
    {
        var token = TestContext.Current.CancellationToken;
        const string credentialName = "EXT_PROTECTION_ROLE_LOOP8_UNACCEPTED";
        string directory = Path.Combine(Path.GetTempPath(), "ext-protection-loop8-unaccepted-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Environment.SetEnvironmentVariable(credentialName, "owned-component-test-bearer");
        Environment.SetEnvironmentVariable("EXT_PROTECTION_ACCEPTED_ROLE_CONFIGURATION_SHA256", new string('0', 64));
        try
        {
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string anchor = Path.Combine(directory, "public.pem");
            await File.WriteAllTextAsync(anchor, signer.ExportSubjectPublicKeyInfoPem(), token);
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var installation = new ProtectionQualificationRoleInstallation("ext-protection-1-role-program/v1", "enrollment", "loop8-enrollment",
                "https://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/platform-protection/v1/role",
                "env:" + credentialName, anchor, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(anchor, token))));
            string path = Path.Combine(directory, "protection-role.enrollment.json");
            await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(installation, ErasureRootCustody.Json), token);
            (await ProtectionQualificationRoleProgram.RunAsync("enrollment", path, token)).ShouldBe(1);
            listener.Pending().ShouldBeFalse();
        }
        finally
        {
            listener.Stop();
            Environment.SetEnvironmentVariable(credentialName, null);
            Environment.SetEnvironmentVariable("EXT_PROTECTION_ACCEPTED_ROLE_CONFIGURATION_SHA256", null);
            Directory.Delete(directory, true);
        }
    }
}
