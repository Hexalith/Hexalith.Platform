using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Shipped Python --apply against an owned native server, followed by the real runtime genesis consumers. C# never installs the script's mounts or heads.</summary>
public sealed class OpenBaoBootstrapIntegrationTests
{
    public static bool Enabled => OpenBaoErasureRootIntegrationTests.Enabled;
    [Fact(Skip = "Requires the owned disposable native bootstrap harness.", SkipUnless = nameof(Enabled))]
    public async Task ShippedApplyInstallsAllExactRuntimeOwnerGenesis()
    {
        var token = TestContext.Current.CancellationToken; using var transport = await OpenBaoDisposableNativeHandler.OpenAsync(token);
        using var nativeHttp = new HttpClient(transport) { BaseAddress = new("https://disposable.invalid/"), Timeout = TimeSpan.FromSeconds(30) };
        using var certificateKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders(); builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        await using var relay = builder.Build();
        relay.MapMethods("/v1/{**path}", ["GET", "POST"], async context =>
        {
            using var forwarded = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path.Value);
            forwarded.Headers.Add("X-Vault-Token", context.Request.Headers["X-Vault-Token"].ToString());
            if (context.Request.Method == "POST") { using var body = new StreamReader(context.Request.Body); forwarded.Content = new StringContent(await body.ReadToEndAsync(context.RequestAborted), Encoding.UTF8, "application/json"); }
            using var response = await nativeHttp.SendAsync(forwarded, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode; await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        });
        string directory = Path.Combine(Path.GetTempPath(), "erc-loop6-bootstrap-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string? runtimeVariable = null;
        try
        {
            await relay.StartAsync(token); string address = relay.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            string ca = Path.Combine(directory, "ca.pem"); await File.WriteAllTextAsync(ca, certificate.ExportCertificatePem(), token);
            var installation = new ErasureRootInstallation("owned-script-erc", "owned-script-enrollment", "erc-transit", "erc-heads", new Dictionary<string, string> { ["tenant-a"] = "epoch-a", ["tenant-b"] = "epoch-b" });
            object InitialDeletion(string tenant)
            {
                var assembly = typeof(DeletionConsumptionActor).Assembly;
                return Activator.CreateInstance(assembly.GetType("Hexalith.EventStore.Server.Security.DeletionConsumptionLedger")!, tenant, 0L, 0L,
                    Array.CreateInstance(assembly.GetType("Hexalith.EventStore.Server.Security.DeletionConsumptionBatch")!, 0), Array.Empty<DeletionCapabilityRevocationReceipt>(),
                    Array.CreateInstance(assembly.GetType("Hexalith.EventStore.Server.Security.DeletionConsumptionOperation")!, 0))!;
            }
            var states = installation.NativeKeys.Keys.ToDictionary(tenant => tenant, tenant => new Dictionary<string, object>
            {
                [ErasureRootOccurrenceAuthority.Scope(tenant)] = new InteractionOccurrenceRegistrySnapshot(tenant, "independent-registry-epoch", 0, []),
                [DeletionConsumptionActor.GetOwnerScope(tenant)] = InitialDeletion(tenant),
                [CustodyKeyLifecycleActor.GetActorId(tenant) + "|custody-key-lifecycle-candidate-v1"] = new CustodyKeyLifecycleLedger(tenant, 0, []),
            });
            var enrollment = new { address, storeId = installation.StoreId, enrollmentId = installation.EnrollmentId, transitMount = installation.TransitMount, headMount = installation.HeadMount,
                nativeKeys = installation.NativeKeys, genesisAnchors = states.ToDictionary(pair => pair.Key, pair => pair.Value.ToDictionary(state => state.Key, state => ErasureRootCustody.Hash(state.Value))) };
            string file = Path.Combine(directory, "enrollment.json"); await File.WriteAllBytesAsync(file, JsonSerializer.SerializeToUtf8Bytes(enrollment), token);
            using var scriptStream = typeof(OpenBaoBootstrapIntegrationTests).Assembly.GetManifestResourceStream("ErcShippedBootstrapScript")!;
            string script = Path.Combine(directory, "bootstrap.py"); await using (var output = File.Create(script)) { await scriptStream.CopyToAsync(output, token); }
            var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { script, "--enrollment", file, "--credential-reference", "env:ERC_NATIVE_DISPOSABLE_TOKEN", "--apply" }) { start.ArgumentList.Add(argument); }
            start.Environment["SSL_CERT_FILE"] = ca;
            using var process = Process.Start(start)!; string result = await process.StandardOutput.ReadToEndAsync(token); string error = await process.StandardError.ReadToEndAsync(token); await process.WaitForExitAsync(token);
            process.ExitCode.ShouldBe(0, "Shipped bootstrap refused"); error.ShouldBeEmpty(); JsonDocument.Parse(result).RootElement.GetProperty("status").GetString().ShouldBe("BootstrappedNotQualified");
            async Task<JsonElement> BootstrapWrite(string path, object body)
            {
                using var operation = new HttpRequestMessage(HttpMethod.Post, path); operation.Headers.Add("X-Vault-Token", Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_TOKEN"));
                operation.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                using var response = await nativeHttp.SendAsync(operation, token); response.IsSuccessStatusCode.ShouldBeTrue();
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token)).RootElement.Clone();
            }
            using var policyStream = typeof(OpenBaoBootstrapIntegrationTests).Assembly.GetManifestResourceStream("ErcShippedCustodyPolicy")!;
            using var policyReader = new StreamReader(policyStream); string policy = await policyReader.ReadToEndAsync(token);
            string policyName = "erc-script-runtime-" + Guid.NewGuid().ToString("N");
            _ = await BootstrapWrite("v1/sys/policies/acl/" + policyName, new { policy });
            var runtimeToken = await BootstrapWrite("v1/auth/token/create", new { policies = new[] { policyName }, no_default_policy = true, ttl = "1h", renewable = false });
            runtimeVariable = "ERC_SCRIPT_RUNTIME_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            Environment.SetEnvironmentVariable(runtimeVariable, runtimeToken.GetProperty("auth").GetProperty("client_token").GetString());
            if (Environment.GetEnvironmentVariable("ERC_NATIVE_DIAGNOSTIC_DIRECTORY") is { } diagnostic)
            {
                string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(policy)));
                await File.WriteAllTextAsync(Path.Combine(diagnostic, "policy-bootstrap.json"), JsonSerializer.Serialize(new { shippedPolicySha256 = digest, renderedPolicySha256 = digest,
                    transitMount = "erc-transit", headMount = "erc-heads", runtimePolicyName = policyName, rootUsedOnlyForBootstrap = true }), token);
            }
            var native = new OpenBaoErasureRootStore(nativeHttp, installation, "env:" + runtimeVariable); var authorization = new ErasureRootAuthorizationFixture(); var custody = new ErasureRootCustody(native, installation, authorization);
            foreach (var tenant in states)
            {
                foreach (var state in tenant.Value)
                {
                    var authority = new ErasureRootTransitionAuthority(custody, authorization, tenant.Key, state.Key);
                    (await authority.ValidateStateAsync(0, ErasureRootCustody.Hash(state.Value), token)).ShouldBeTrue();
                    (await authority.ValidateStateAsync(0, new string('0', 64), token)).ShouldBeFalse();
                }
                var owner = new ErasureRootDeletionOwnerAuthority(custody, authorization, tenant.Key);
                (await owner.ValidateStateAsync(0, ErasureRootCustody.Hash(InitialDeletion(tenant.Key)), token)).ShouldBeTrue();
                var lifecycle = new ErasureRootLifecycleAuthority(custody, authorization, new(custody, authorization, tenant.Key, CustodyKeyLifecycleActor.GetActorId(tenant.Key) + "|custody-key-lifecycle-candidate-v1"));
                var actor = new CustodyKeyLifecycleActor(ActorHost.CreateForTest<CustodyKeyLifecycleActor>(new ActorTestOptions { ActorId = new(CustodyKeyLifecycleActor.GetActorId(tenant.Key)) }), lifecycle, custody);
                typeof(Actor).GetProperty("StateManager", BindingFlags.Instance | BindingFlags.Public)!.SetValue(actor, new InMemoryStateManager());
                var wrapped = (await custody.WrapAsync(new(tenant.Key, "bootstrap-root", PlatformKeyPurpose.InteractionRootDek, "bootstrap-alias", "root-v1", "lifecycle", installation.StoreId, "custody"), "bootstrap-wrap", 0, token))!;
                (await actor.RegisterWrappedAsync(wrapped)).Status.ShouldBe(CustodyKeyLifecycleStatus.Wrapped);
            }
            Console.WriteLine(JsonSerializer.Serialize(new { shippedPythonApply = true, tenants = 2, runtimeGenesisScopes = 6, actualLifecycleActors = 2, ordinaryMounts = true, productionQualified = false }));
        }
        finally { if (runtimeVariable is not null) { Environment.SetEnvironmentVariable(runtimeVariable, null); } await relay.StopAsync(CancellationToken.None); Directory.Delete(directory, true); }
    }
}
