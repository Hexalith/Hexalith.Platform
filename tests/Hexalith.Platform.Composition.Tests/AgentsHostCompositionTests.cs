namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Dapr.Client;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Platform.Composition;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

/// <summary>Agents enablement stays refused without an accepted profile, and spool loss blocks target commands.</summary>
public sealed class AgentsHostCompositionTests
{
    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs")))
                directory = directory.Parent;
            return directory!.FullName;
        }
    }

    /// <summary>Enablement without a profile keeps the existing refusal and adds no resource.</summary>
    [Fact]
    public void AbsentProfileRefusesBeforeResources()
    {
        IDistributedApplicationBuilder builder = Builder();
        builder.Configuration["Platform:Agents:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message
            .ShouldBe("DependencyNotAvailable: EXT-HOST-1. Full Agents composition and accepted owner prerequisites are unavailable.");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>Empty replay, recorder, worker, or spool slots install nothing.</summary>
    [Fact]
    public void EmptyCredentialSlotsRefuseBeforeResources()
    {
        string profile = Path.GetTempFileName();
        try
        {
            JsonObject document = CompleteProfile(Root);
            document["replay"] = "";
            document["recorder"] = "";
            document["worker"] = "";
            document["spoolBackend"] = "";
            File.WriteAllText(profile, document.ToJsonString());
            AgentsDeploymentProfile.MissingReferences(document.ToJsonString()).ShouldContain("replay");
            AgentsDeploymentProfile.MissingReferences(document.ToJsonString()).ShouldContain("recorder");
            AgentsDeploymentProfile.MissingReferences(document.ToJsonString()).ShouldContain("worker");
            AgentsDeploymentProfile.MissingReferences(document.ToJsonString()).ShouldContain("spoolBackend");
            IDistributedApplicationBuilder builder = Builder();
            builder.Configuration["Platform:Agents:Enabled"] = "true";
            builder.Configuration[AgentsHostComposition.ProfilePathKey] = profile;
            Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("EXT-HOST-1");
            builder.Resources.ShouldBeEmpty();
        }
        finally { File.Delete(profile); }
    }

    /// <summary>A missing sibling checkout fails composition before resources exist.</summary>
    [Fact]
    public void MissingSiblingCheckoutFailsBeforeResources()
    {
        string root = Path.Combine(Path.GetTempPath(), "agents-missing-" + Guid.NewGuid().ToString("N"));
        string profile = Path.GetTempFileName();
        try
        {
            JsonObject document = CompleteProfile(root);
            File.WriteAllText(profile, document.ToJsonString());
            IDistributedApplicationBuilder builder = Builder();
            builder.Configuration["Platform:Agents:Enabled"] = "true";
            builder.Configuration[AgentsHostComposition.ProfilePathKey] = profile;
            Should.Throw<FileNotFoundException>(() => PlatformAppModel.ComposeEntry(builder)).FileName.ShouldStartWith(root);
            builder.Resources.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(profile);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    /// <summary>Secrets, operator kubeconfig, OpenBao server secrets, and unapproved scope keys are rejected.</summary>
    [Theory]
    [InlineData("replay", "/home/administrator/.kube/hexalith-production")]
    [InlineData("recorder", "openbao/Secret/openbao-server-tls")]
    [InlineData("worker", "openbao/Secret/openbao-seal")]
    [InlineData("humanExactConversationScope", "approved")]
    [InlineData("classUtcRangeScope", "approved")]
    public void SecretsAndUnapprovedScopesAreRejected(string key, string value)
    {
        JsonObject document = CompleteProfile(Root);
        document[key] = value;
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.Validate(document.ToJsonString()));
        refusal.Message.ShouldNotContain(value);
    }

    /// <summary>A ready profile binds sibling projects and the tenants global administrator reference without registering raw credentials.</summary>
    [Fact]
    public async Task ValidProfileComposesSiblingServicesAndBindsAdministratorReference()
    {
        string root = Path.Combine(Path.GetTempPath(), "agents-compose-" + Guid.NewGuid().ToString("N"));
        string profile = Path.GetTempFileName();
        try
        {
            foreach ((string directory, string name) in new (string, string)[]
            {
                ("src/Hexalith.EventStore", "Hexalith.EventStore.csproj"),
                ("src/Hexalith.Tenants", "Hexalith.Tenants.csproj"),
                ("src/Hexalith.Tenants.Api", "Hexalith.Tenants.Api.csproj"),
                ("src/Hexalith.Parties", "Hexalith.Parties.csproj"),
                ("src/Hexalith.Conversations.Server", "Hexalith.Conversations.Server.csproj"),
                ("src/Hexalith.Agents.Server", "Hexalith.Agents.Server.csproj"),
                ("src/Hexalith.Agents.UI", "Hexalith.Agents.UI.csproj"),
            })
            {
                string folder = Path.Combine(root, directory);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, name), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            }
            File.WriteAllText(profile, CompleteProfile(root).ToJsonString());
            IDistributedApplicationBuilder builder = Builder();
            builder.Configuration["Platform:Agents:Enabled"] = "true";
            builder.Configuration[AgentsHostComposition.ProfilePathKey] = profile;
            PlatformAppModel.ComposeEntry(builder);
            builder.Resources.OfType<ProjectResource>().Select(resource => resource.Name).Order()
                .ShouldBe(["agents", "conversations", "eventstore", "parties", "tenants", "tenants-api"]);
            builder.Resources.ShouldNotContain(resource => resource.Name.Contains("AppHost", StringComparison.Ordinal)
                || resource.Name.Contains("ServiceDefaults", StringComparison.Ordinal));
            ProjectResource tenants = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "tenants");
            Dictionary<string, object> environment = await EnvironmentAsync(builder, tenants);
            environment["Tenants__BootstrapGlobalAdminEnabled"].ShouldBe("true");
            environment["Tenants__BootstrapGlobalAdminUserId"].ShouldBe("ref:global-administrator");
            environment["Agents__Replay__Reference"].ShouldBe("ref:replay");
            builder.Services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ReplicatedSecurityObservationSpool));
            builder.Services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ITrustedEnvelopeReplayRegistrar));
        }
        finally
        {
            File.Delete(profile);
            Directory.Delete(root, true);
        }
    }

    /// <summary>A signed operation registers issuer and nonce before the target command is built.</summary>
    [Fact]
    public async Task SignedAdmissionRegistersIssuerBeforeTargetCommand()
    {
        AgentsSpoolHarness harness = new();
        (await harness.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        ManualClock clock = harness.Clock;
        PlatformSigningProfile profile = new("profile-v1", "issuer-a", "audience-a", clock.Now.AddDays(-1), clock.Now.AddDays(1),
            TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(86400), TimeSpan.FromSeconds(604800));
        IPlatformSigningProfileProvider profiles = Substitute.For<IPlatformSigningProfileProvider>();
        profiles.GetCurrent().Returns(profile);
        IPlatformHmacKeyProvider keys = Substitute.For<IPlatformHmacKeyProvider>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            byte[] material = Enumerable.Repeat((byte)11, 32).ToArray();
            try
            {
                PlatformHmacKeySnapshot snapshot = new(call.Arg<PlatformHmacScope>(), new PlatformHmacKeyMetadata(
                    "key-v1", PlatformHmacKeyState.Active, clock.Now.AddDays(-10), clock.Now.AddDays(10), null), material);
                return new PlatformHmacKeyResolution(CustodyStatus.Succeeded, snapshot);
            }
            finally { CryptographicOperations.ZeroMemory(material); }
        });
        TrustedEnvelopeAuthenticator authenticator = new(keys, profiles, clock);
        TrustedEnvelopeIdentity expected = new(1, "profile-v1", "issuer-a",
            new TrustedPrincipal(TrustedPrincipalKind.User, "tenant-a", "actor-a", "party-a", 1, "roles", null, null, null, null),
            "Contracts.EditProposal", "EditProposal", "tenant-a", "resource-a", "correlation", "cause",
            ["tenant-a"], "fingerprint", "digest-v1", "audience-a", "logical-command");
        TrustedEnvelope envelope = (await authenticator.IssueAsync(expected, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken)).Envelope!;
        ITrustedEnvelopeReplayRegistrar registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>();
        int order = 0;
        TrustedEnvelopeReplayReceipt? stored = null;
        registrar.RegisterAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                order.ShouldBe(0);
                order = 1;
                TrustedEnvelopeReplayIntent intent = call.Arg<TrustedEnvelopeReplayIntent>();
                intent.Issuer.ShouldBe("issuer-a");
                intent.DeliveryNonce.ShouldBe(envelope.DeliveryNonce);
                stored = new(intent, call.Arg<DateTimeOffset>(), call.Arg<DateTimeOffset>() + call.Arg<TimeSpan>(), 1, "original-replay-receipt");
                return stored;
            });
        registrar.LookupAsync(Arg.Any<TrustedEnvelopeReplayIntent>(), Arg.Any<CancellationToken>())
            .Returns(call => stored is not null && stored.Intent == call.Arg<TrustedEnvelopeReplayIntent>() ? stored : null);
        TrustedEnvelopeReplayVerifier verifier = new(authenticator, registrar, profiles, clock);
        AgentsAdmissionResult result = await AgentsHostComposition.AdmitAsync(verifier, harness.Spool, envelope, expected, _ =>
        {
            order.ShouldBe(1);
            return "target-command";
        }, TestContext.Current.CancellationToken);
        result.CommandConstructed.ShouldBeTrue();
        result.Command.ShouldBe("target-command");
        order.ShouldBe(1);
    }

    /// <summary>A spool that is not ready blocks target-command construction and writes nothing.</summary>
    [Fact]
    public async Task SpoolFailureBlocksTargetCommand()
    {
        ITrustedEnvelopeReplayRegistrar registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>();
        TrustedEnvelopeReplayVerifier verifier = Verifier(registrar);
        ReplicatedSecurityObservationSpool spool = new(Substitute.For<DaprClient>(), TimeProvider.System);
        int constructed = 0;
        AgentsAdmissionResult result = await AgentsHostComposition.AdmitAsync(verifier, spool, Envelope(), Identity(), _ =>
        {
            constructed++;
            return "command";
        }, TestContext.Current.CancellationToken);
        result.CommandConstructed.ShouldBeFalse();
        result.Command.ShouldBe("SecurityAuditUnavailable");
        constructed.ShouldBe(0);
        registrar.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A lost acknowledgement leaves the original pending, appends one security event, and builds no command.</summary>
    [Fact]
    public async Task LostAcknowledgementRecoversOneEventAndBuildsNoCommand()
    {
        AgentsSpoolHarness harness = new();
        SecurityObservationRecord? observed = await harness.Spool.ObserveAsync(AgentsSpoolHarness.Intent(), TestContext.Current.CancellationToken);
        observed.ShouldNotBeNull();
        int lookups = 0;
        int appends = 0;
        harness.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int call = Interlocked.Increment(ref lookups);
            return call == 1
                ? new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded)
                : new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown);
        });
        harness.Recorder.AppendAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Interlocked.Increment(ref appends);
            return Task.FromException(new HttpRequestException("lost acknowledgement"));
        });
        int constructed = 0;
        AgentsAdmissionResult blocked = await AgentsHostComposition.AdmitAsync(Verifier(Substitute.For<ITrustedEnvelopeReplayRegistrar>()), harness.Spool, Envelope(), Identity(), _ =>
        {
            constructed++;
            return "command";
        }, TestContext.Current.CancellationToken);
        blocked.CommandConstructed.ShouldBeFalse();
        blocked.Command.ShouldBe("SecurityAuditUnavailable");
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        AgentsHostComposition.BindPrivateCapabilities(services, new AgentsPrivateCapabilities(
            Substitute.For<ITrustedEnvelopeReplayRegistrar>(), harness.Spool, TimeSpan.FromHours(1), 1));
        await using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull();
        provider.GetService<ITrustedEnvelopeReplayRegistrar>().ShouldBeNull();
        IHostedService worker = provider.GetServices<IHostedService>().Single();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitAsync(() => lookups >= 2 && appends == 1);
            harness.Read()!.Records.Single().Receipt.ShouldBeNull();
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
        ReplicatedSecurityObservationSpoolWorker restarted = new(harness.Spool, TimeProvider.System, TimeSpan.FromHours(1), 1);
        await restarted.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitAsync(() => lookups >= 3);
            appends.ShouldBe(1);
            harness.Read()!.Records.Single().Receipt.ShouldBeNull();
            constructed.ShouldBe(0);
        }
        finally { await restarted.StopAsync(TestContext.Current.CancellationToken); }
    }

    private static async Task WaitAsync(Func<bool> ready)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(5);
        while (!ready() && DateTime.UtcNow < until)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        ready().ShouldBeTrue();
    }

    private static TrustedEnvelopeReplayVerifier Verifier(ITrustedEnvelopeReplayRegistrar registrar) => new(
        new TrustedEnvelopeAuthenticator(Substitute.For<IPlatformHmacKeyProvider>(), Substitute.For<IPlatformSigningProfileProvider>(), TimeProvider.System),
        registrar, Substitute.For<IPlatformSigningProfileProvider>(), TimeProvider.System);

    private static TrustedEnvelopeIdentity Identity() => new(1, "profile", "issuer",
        new TrustedPrincipal(TrustedPrincipalKind.User, "tenant-a", "actor-a", "party-a", 1, "roles", null, null, null, null),
        "Contracts.EditProposal", "EditProposal", "tenant-a", "resource-a", "correlation", "cause",
        ["tenant-a"], "fingerprint", "digest-v1", "audience", "logical-command");

    private static TrustedEnvelope Envelope()
    {
        DateTimeOffset issued = DateTimeOffset.UnixEpoch.AddYears(50);
        return new TrustedEnvelope(Identity(), issued, issued.AddMinutes(2), "nonce", "key-v1", new string('a', 64));
    }

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationBuilder builder, IResource resource)
    {
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken));
        return environment;
    }

    private static JsonObject CompleteProfile(string root) => new()
    {
        ["host"] = "192.168.1.30",
        ["integrationDate"] = "2026-10-08",
        ["partiesBranch"] = "B",
        ["timing"] = new JsonObject { ["L"] = 300, ["S"] = 30, ["O"] = 600, ["H"] = 86400, ["R"] = 604800 },
        ["services"] = new JsonObject
        {
            ["eventStore"] = root, ["tenants"] = root, ["parties"] = root, ["conversations"] = root, ["agents"] = root,
        },
        ["currentTenant"] = "ref:current-tenant",
        ["globalAdministrator"] = "ref:global-administrator",
        ["humanIdentity"] = "ref:human-identity",
        ["approverEvidence"] = "ref:approver-evidence",
        ["replay"] = "ref:replay",
        ["recorder"] = "ref:recorder",
        ["worker"] = "ref:worker",
        ["spoolBackend"] = "ref:spool-backend",
        ["replicaCount"] = 3,
        ["failureModel"] = "ref:failure-model",
        ["protectionEngine"] = "ref:protection-engine",
        ["protectionObserver"] = "ref:protection-observer",
        ["partiesBinding"] = "ref:parties-binding",
        ["conversationsBinding"] = "ref:conversations-binding",
    };

    private static IDistributedApplicationBuilder Builder()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, ProjectDirectory = Root,
            DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        builder.Environment.EnvironmentName = "Development";
        return builder;
    }
}
