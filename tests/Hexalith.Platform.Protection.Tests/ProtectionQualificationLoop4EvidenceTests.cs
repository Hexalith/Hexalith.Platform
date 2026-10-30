using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual source-owned guard/owner projections, raw typed integer role signing, real HTTP and offline Python proof verification.</summary>
public sealed class ProtectionQualificationLoop4EvidenceTests
{
    [Theory]
    [InlineData("P20", true)]
    [InlineData("P21", true)]
    [InlineData("P21", false)]
    public async Task RealHoldSourceOriginalsBindSignedOuterTenantDeletionScopeAndCompleteTargets(string caseId, bool holdWins)
    {
        using var fixture = new ProtectionQualificationPreparationFixture(caseId == "P21" ? holdWins : null); await fixture.InitializeAsync();
        if (caseId == "P20") { await fixture.SealExpectedSourcesAsync(caseId); }
        var recipes = fixture.Recipes(caseId)[caseId];
        if (caseId == "P20")
        { await fixture.RunAsync(recipes["HoldBeforeDispatch"], caseId, "HoldBeforeDispatch"); await fixture.RunAsync(recipes["RejectManifestDestroy"], caseId, "RejectManifestDestroy"); }
        else { await fixture.RunAsync(recipes["RaceHoldDispatch"], caseId, "RaceHoldDispatch"); }
        var head = (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value;
        var calls = new List<ProtectionQualificationNativeOriginal>();
        foreach (var call in fixture.Admitted.Values.Where(call => call.Kind is ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch))
        {
            var result = fixture.Originals[call.Id]!; calls.Add(new(call, result));
            if (result["IntentDigest"] is null) { continue; }
            var transition = call.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!; var receipt = result.Deserialize<GovernanceProtocolReceipt>()!;
            head.Head.GuardAttempts[receipt.IntentDigest] = new(transition, ErasureRootCustody.Hash(call.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!), transition.ExpectedGuardRevision, receipt.GuardHighWater, receipt);
        }
        var observed = new JsonObject();
        ProtectionQualificationNativeProjection.ProjectHolds(observed, head.Head, calls, (JsonObject)JsonSerializer.SerializeToNode(fixture.Guard.State)!);
        await VerifyPythonAsync(new { kind = "hold", observation = observed, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem });
        if (caseId == "P20")
        {
            (await fixture.Consumption.Custody.SaveAsync("tenant-a", head.Version, head.Head, TestContext.Current.CancellationToken)).ShouldBeTrue();
            var complete = await fixture.ObserveAsync(caseId);
            await VerifyPythonAsync(new { kind = "no-effect", observation = complete, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem });
        }
    }
    [Theory]
    [InlineData("P05")]
    [InlineData("P29")]
    public async Task NativeTypedProviderAndActiveCredentialIntegersSignThroughActualRoleHttpAndPython(string caseId)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        var recipes = fixture.Recipes(caseId);
        if (caseId == "P29")
        {
            await fixture.RunAsync(recipes[caseId]["CompromiseAndReattest"], caseId, "CompromiseAndReattest");
            await fixture.RunAsync(recipes[caseId]["ActivateReattestedDeletionBatch"], caseId, "ActivateReattestedDeletionBatch");
        }
        await fixture.RunAsync(recipes[caseId]["DestroyDekManifest"], caseId, "DestroyDekManifest");
        string binding = new('a', 64); var producer = ProtectionQualificationProducerIdentity.Capture(fixture.Engine.Engine, fixture.Consumption.Custody.Installation, "component-executor", binding);
        var selected = (await fixture.Consumption.Custody.ReadAsync("tenant-a", token, true))!.Value; var expected = new JsonArray();
        selected.Head.QualificationAttempts[binding] = "component-attempt";
        foreach (var entry in selected.Head.QualificationOperations.ToArray())
        {
            var safe = (JsonObject)entry.Value.SafeRequest.DeepClone(); safe["operationId"] = entry.Key;
            var operation = entry.Value with { SafeRequest = safe, RequestSha256 = ProtectionQualificationOperationJournal.Digest(safe), Result = new JsonObject { ["status"] = "Completed" }, Producer = producer };
            selected.Head.QualificationOperations.Remove(entry.Key); selected.Head.QualificationOperations.Add(ProtectionQualificationOperationJournal.Id("executor", entry.Key), operation);
            expected.Add(new JsonObject { ["operationId"] = entry.Key, ["requestSha256"] = operation.RequestSha256, ["action"] = safe["action"]!.DeepClone() });
        }
        (await fixture.Consumption.Custody.SaveAsync("tenant-a", selected.Version, selected.Head, token)).ShouldBeTrue();
        fixture.NativeState["installation"] = JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-v23", deploymentIdentity = "component-observer", credentialScope = "ReadPersistedStateOnly" });
        var observations = recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new() { ["guard"] = new("source", "guard", new()) }, new()) { CaseId = id });
        var scope = new ProtectionQualificationServiceScope("observer", "component-observer", "tenant-a", binding, new("source", "installation", new()), recipes, observations);
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var observer = new ProtectionQualificationPersistedObserver(fixture.Native, fixture.Consumption.Custody, fixture.Engine.Engine, new HashSet<string> { "source" });
        var service = new ProtectionQualificationRoleService(scope, new(fixture.Consumption.Custody, fixture.Consumption.Permissions), null, observer, fixture.Native, signer,
            observerPermission: _ => Task.FromResult<ProtectionQualificationObserverPermission?>(new("tenant-a", "erc-v23", "enrollment", "component", scope.Identity, binding, "native", "dapr", "role", "ReadPersistedStateOnly", [], [], [], [], DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1), "independently-verified-component-permission") { ComponentBackings = new() { ["source"] = "synthetic-native-source" } }));
        string variable = "LOOP4_ROLE_" + Guid.NewGuid().ToString("N").ToUpperInvariant(); Environment.SetEnvironmentVariable(variable, "synthetic-role-credential");
        await using var app = ProtectionQualificationRoleApplication.Build(service, "env:" + variable); app.Urls.Add("http://127.0.0.1:0");
        try
        {
            await app.StartAsync(token); string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient { BaseAddress = new(address) }; using var request = new HttpRequestMessage(HttpMethod.Post, "/platform-protection/v1/role");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-role-credential"); request.Headers.Add("X-Hexalith-Protection-Role-Contract", "ext-protection-1-role-program/v1");
            request.Content = new StringContent(new JsonObject { ["operationId"] = "observe", ["action"] = "ObservePersistedState", ["bindingDigest"] = binding, ["caseId"] = caseId, ["fixtureId"] = caseId + "-fixture", ["targets"] = JsonSerializer.SerializeToNode(fixture.Targets), ["expectedOperations"] = expected }.ToJsonString(), Encoding.UTF8, "application/json");
            string requestBody = await request.Content.ReadAsStringAsync(token);
            int archiveReads = fixture.Consumption.Store.ArchiveReads; long archiveBytes = fixture.Consumption.Store.ArchiveReadBytes;
            using var response = await http.SendAsync(request, token); response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var packet = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
            await VerifyPythonAsync(new { kind = "role", caseId, response = packet, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem, rolePublicKeyPem = signer.ExportSubjectPublicKeyInfoPem() });
            if (caseId == "P05")
            {
                int baselineReads = fixture.Consumption.Store.ArchiveReads - archiveReads; long baselineBytes = fixture.Consumption.Store.ArchiveReadBytes - archiveBytes;
                // Actual owner/ERC transitions retain unrelated prior requests for these same targets. Private eligibility is the explicit component boundary.
                fixture.Consumption.Authority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(true);
                var original = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.Register).Payload.Deserialize<DeletionBatchConsumptionRequest>()!;
                for (int index = 0; index < 40; index++)
                {
                    var unrelated = original with { Capability = original.Capability with { BatchId = ErasureRootCustody.Hash("unrelated-batch-" + index), DeletionRequestId = "unrelated-request-" + index } };
                    (await fixture.Consumption.Actor.RegisterAsync(unrelated)).Status.ShouldBe(DeletionConsumptionStatus.AlreadyDestroyedByBatch);
                }
                var history = (await fixture.Consumption.Custody.ReadAsync("tenant-a", token, true))!.Value;
                for (int index = 0; index < 128; index++)
                {
                    var transition = new GovernanceGuardTransition("tenant-a", "unrelated-guard-" + index, GovernanceGuardOperation.RegisterHold, index, "epoch", "unrelated-request", null, null, null, null, null, null, new string('x', 2048), "");
                    string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()) });
                    history.Head.GuardAttempts.Add(intent, new(transition, ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()), index, index + 1, new(transition.OperationId, intent, "Stale", index + 1, 0, "", "unrelated-refusal-" + index)));
                }
                (await fixture.Consumption.Custody.SaveAsync("tenant-a", history.Version, history.Head, token)).ShouldBeTrue();
                archiveReads = fixture.Consumption.Store.ArchiveReads; archiveBytes = fixture.Consumption.Store.ArchiveReadBytes;
                using var repeatedRequest = new HttpRequestMessage(HttpMethod.Post, "/platform-protection/v1/role");
                repeatedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-role-credential"); repeatedRequest.Headers.Add("X-Hexalith-Protection-Role-Contract", "ext-protection-1-role-program/v1");
                repeatedRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using var repeated = await http.SendAsync(repeatedRequest, token); repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
                var second = JsonNode.Parse(await repeated.Content.ReadAsStringAsync(token))!;
                JsonNode.DeepEquals(second["statement"]!["response"], packet["statement"]!["response"]).ShouldBeTrue();
                (fixture.Consumption.Store.ArchiveReads - archiveReads).ShouldBeLessThanOrEqualTo(baselineReads + 30);
                (fixture.Consumption.Store.ArchiveReadBytes - archiveBytes).ShouldBeLessThan(baselineBytes + 1024 * 1024);
                await VerifyPythonAsync(new { kind = "role", caseId, response = second, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem, rolePublicKeyPem = signer.ExportSubjectPublicKeyInfoPem() });
                Console.WriteLine(JsonSerializer.Serialize(new { observerUnrelatedSameTargetBatches = 40, unrelatedGuardOriginals = 128, baselineReads, baselineBytes,
                    finalReads = fixture.Consumption.Store.ArchiveReads - archiveReads, finalBytes = fixture.Consumption.Store.ArchiveReadBytes - archiveBytes }));
            }

        }
        finally { await app.StopAsync(CancellationToken.None); Environment.SetEnvironmentVariable(variable, null); }
    }
    private static async Task VerifyPythonAsync(object packet)
    {
        string root = Path.GetDirectoryName(typeof(ProtectionQualificationLoop4EvidenceTests).Assembly.Location)!;
        while (!File.Exists(Path.Combine(root, "eng/ext-protection-1/verify_loop4_vector.py"))) { root = Directory.GetParent(root)!.FullName; }
        var start = new ProcessStartInfo("python3") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(root, "eng/ext-protection-1/verify_loop4_vector.py")); start.ArgumentList.Add(Path.GetFullPath(Path.Combine(root, "../eventstore")));
        using var process = Process.Start(start)!; await process.StandardInput.WriteAsync(JsonSerializer.Serialize(packet)); process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken), error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken); process.ExitCode.ShouldBe(0, output + error);
    }
}
