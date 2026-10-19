using System.Diagnostics.Metrics;
using System.Security.Claims;

using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Platform.Works.Runtime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Platform.Works.Tests;

/// <summary>Operator binding, durable audit ordering and actual cached metric observations.</summary>
public sealed class RecoveryRuntimeTests
{
    private static readonly ProcessSource _source = new("synthetic-416", "work", "item-1");

    /// <summary>All protected resource and attribution claims are mandatory and exact.</summary>
    [Theory]
    [InlineData(ClaimTypes.NameIdentifier)]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.OperationClaimType)]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType)]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType)]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "aggregate")]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "purpose")]
    [InlineData(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType)]
    public async Task MissingAndWrongBindingRefuses(string claimType)
    {
        var authorizer = new WorksSyntheticDispositionAuthorizer([_source], "eventstore-admin");
        ClaimsPrincipal principal = Principal();
        (await authorizer.AuthorizeAsync(principal, _source, TestContext.Current.CancellationToken)).ShouldBeTrue();
        ClaimsIdentity identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(claimType)!);
        (await authorizer.AuthorizeAsync(principal, _source, TestContext.Current.CancellationToken)).ShouldBeFalse();
        identity.AddClaim(new Claim(claimType, claimType.EndsWith("actor", StringComparison.Ordinal) ? " " : "foreign"));
        (await authorizer.AuthorizeAsync(principal, _source, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Anonymous, duplicate identity and cross-source requests cannot inherit a fixture grant.</summary>
    [Fact]
    public async Task AnonymousAmbiguousAndCrossSourceRefuse()
    {
        List<ProcessSource> grants = [_source];
        var authorizer = new WorksSyntheticDispositionAuthorizer(grants, "eventstore-admin");
        grants.Add(_source with { Aggregate = "later-added" });
        (await authorizer.AuthorizeAsync(new ClaimsPrincipal(), _source, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authorizer.AuthorizeAsync(Principal(), _source with { Tenant = "foreign" }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authorizer.AuthorizeAsync(Principal(), grants[1], TestContext.Current.CancellationToken)).ShouldBeFalse();
        ClaimsPrincipal duplicateClaim = Principal();
        ((ClaimsIdentity)duplicateClaim.Identity!).AddClaim(new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, _source.Tenant));
        (await authorizer.AuthorizeAsync(duplicateClaim, _source, TestContext.Current.CancellationToken)).ShouldBeFalse();
        ClaimsPrincipal twoIdentities = Principal();
        twoIdentities.AddIdentity((ClaimsIdentity)Principal().Identity!);
        (await authorizer.AuthorizeAsync(twoIdentities, _source, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>No empty, repeated, unbounded, or malformed synthetic grant set is admitted.</summary>
    [Fact]
    public void GrantPopulationIsBoundedAndCanonical()
    {
        Should.Throw<ArgumentException>(() => new WorksSyntheticDispositionAuthorizer([], "eventstore-admin"));
        Should.Throw<ArgumentException>(() => new WorksSyntheticDispositionAuthorizer([_source, _source], "eventstore-admin"));
        Should.Throw<ArgumentException>(() => new WorksSyntheticDispositionAuthorizer(Enumerable.Range(0, 65).Select(i => _source with { Aggregate = "item-" + i }), "eventstore-admin"));
        Should.Throw<ArgumentException>(() => new WorksSyntheticDispositionAuthorizer([_source with { Tenant = "NONCANONICAL" }], "eventstore-admin"));
    }

    /// <summary>Real SDK mutation follows persisted attributed audit; audit outage leaves all control state intact.</summary>
    [Fact]
    public async Task AuditOutageRefusesBeforeSdkMutationAndSuccessPersistsBeforeRevalidation()
    {
        RecoveryRuntimeMemoryStore store = new();
        var options = Options.Create(new EventStoreProcessOptions());
        var checkpoints = new ProcessCheckpointStore(store, options, TimeProvider.System);
        var sweeps = new ProcessSweepStore(store, options, TimeProvider.System);
        await checkpoints.EnsureCandidateAsync(_source, TestContext.Current.CancellationToken);
        ProcessSourceState owned = (await checkpoints.AcquireAsync(_source, TestContext.Current.CancellationToken))!;
        owned = await checkpoints.UpdateOwnedAsync(owned, state => state with
        {
            Status = ProcessSourceStatus.Quarantined, ReasonCode = "invalid-evidence", FailureSequence = 1,
        }, TestContext.Current.CancellationToken);
        await checkpoints.ReleaseAsync(owned, TestContext.Current.CancellationToken);
        ProcessSourceState before = await checkpoints.GetAsync(_source, TestContext.Current.CancellationToken);
        var authorizer = new WorksSyntheticDispositionAuthorizer([_source], "eventstore-admin");
        var audit = new WorksSyntheticDispositionAuditSink(store, authorizer, "statestore", TimeProvider.System);
        var disposition = new ProcessDispositionService(authorizer, audit, checkpoints, sweeps, options);
        store.Writes.Clear();
        store.AuditUnavailable = true;
        await Should.ThrowAsync<IOException>(() => disposition.RevalidateAsync(Principal(), _source, "synthetic repair", TestContext.Current.CancellationToken));
        store.Writes.ShouldBeEmpty();
        (await checkpoints.GetAsync(_source, TestContext.Current.CancellationToken)).ShouldBe(before);
        (await sweeps.GetAsync(TestContext.Current.CancellationToken)).Pending.ShouldBeEmpty();
        store.AuditUnavailable = false;
        store.AuditConflicts = 2;
        (await disposition.RevalidateAsync(Principal(), _source, "synthetic repair", TestContext.Current.CancellationToken)).ShouldBeTrue();
        store.Writes[0].ShouldBe(WorksSyntheticDispositionAuditSink.Key(_source));
        var persisted = (await store.GetAsync<WorksProcessDispositionAuditDocument>("statestore", store.Writes[0], TestContext.Current.CancellationToken)).Value!;
        persisted.Attempts.Count.ShouldBe(1);
        persisted.Attempts[0].Actor.ShouldBe("fixture-operator");
        persisted.Attempts[0].Workload.ShouldBe("eventstore-admin");
        persisted.Attempts[0].Source.ShouldBe(_source);
        persisted.Attempts[0].Purpose.ShouldBe("works.process-disposition");
        persisted.Attempts[0].Reason.ShouldBe("synthetic repair");
        (await new ProcessCheckpointStore(store, options, TimeProvider.System).GetAsync(_source, TestContext.Current.CancellationToken)).Status.ShouldBe(ProcessSourceStatus.Revalidating);
    }

    /// <summary>Direct sink use cannot bypass scope validation or retain oversized operator text.</summary>
    [Fact]
    public async Task AuditSinkAlsoRefusesUnauthorizedAndOversizedAttempts()
    {
        RecoveryRuntimeMemoryStore store = new();
        var sink = new WorksSyntheticDispositionAuditSink(store, new WorksSyntheticDispositionAuthorizer([_source], "eventstore-admin"), "statestore", TimeProvider.System);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => sink.RecordAsync(new ClaimsPrincipal(), _source, "repair", TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => sink.RecordAsync(Principal(), _source, new string('a', 257), TestContext.Current.CancellationToken));
        store.Writes.ShouldBeEmpty();
    }

    /// <summary>Actual MeterListener output uses canonical instruments and never emits source identifiers as labels.</summary>
    [Fact]
    public async Task MetricsAndAlertTrackSdkRecoveryAndStaleOrAbsentReports()
    {
        using var diagnostics = new EventStoreDomainDiagnostics("work");
        RecoveryRuntimeTimeProvider time = new();
        var publisher = new WorksRecoveryTelemetryPublisher(diagnostics, time);
        List<(string Name, long Value, string[] Tags)> observed = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, active) =>
        {
            if (ReferenceEquals(instrument.Meter, diagnostics.Meter)) active.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => observed.Add((instrument.Name, value, tags.ToArray().Select(tag => tag.Key + "=" + tag.Value).ToArray())));
        listener.Start();
        publisher.GetSnapshot().Status.ShouldBe(HealthStatus.Unhealthy);
        await publisher.PublishAsync(Report(HealthStatus.Degraded, new Dictionary<string, object>
        {
            ["complete"] = false, ["unresolved"] = 3, ["pendingDispositions"] = 1, ["capacityExceeded"] = true,
            ["reasonCounts"] = new Dictionary<string, int> { ["source-incomplete"] = 2, ["tenant-secret-item-payload"] = 1 },
        }), TestContext.Current.CancellationToken);
        listener.RecordObservableInstruments();
        observed.ShouldContain(value => value.Name == "eventstore.domain.recovery.alert" && value.Value == 1);
        observed.ShouldContain(value => value.Name == "eventstore.domain.recovery.unresolved" && value.Value == 3);
        observed.ShouldContain(value => value.Name == "eventstore.domain.recovery.reason" && value.Value == 1 && value.Tags.Contains("reason=other"));
        observed.SelectMany(value => value.Tags).ShouldNotContain(value => value.Contains("tenant-secret", StringComparison.Ordinal));
        publisher.GetSnapshot().Status.ShouldBe(HealthStatus.Degraded);
        await publisher.PublishAsync(Report(HealthStatus.Healthy, new Dictionary<string, object> { ["complete"] = true, ["unresolved"] = 0 }), TestContext.Current.CancellationToken);
        publisher.GetSnapshot().Status.ShouldBe(HealthStatus.Healthy);
        time.Now += TimeSpan.FromSeconds(16);
        publisher.GetSnapshot().Status.ShouldBe(HealthStatus.Unhealthy);
        await publisher.PublishAsync(new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero), TestContext.Current.CancellationToken);
        publisher.GetSnapshot().Status.ShouldBe(HealthStatus.Unhealthy);
        await publisher.PublishAsync(Report(HealthStatus.Healthy, new Dictionary<string, object> { ["complete"] = true, ["unresolved"] = 1 }), TestContext.Current.CancellationToken);
        publisher.GetSnapshot().Status.ShouldBe(HealthStatus.Degraded);
    }

    /// <summary>Production cannot install the qualification authority even if fixture markers exist.</summary>
    [Fact]
    public void ProductionCannotInstallSyntheticAuthority()
    {
        ServiceCollection services = new();
        IHostEnvironment environment = new HostingEnvironment { EnvironmentName = Environments.Production };
        Should.Throw<InvalidOperationException>(() => services.AddWorksSyntheticRecoveryRuntime(environment, [_source], "eventstore-admin", "statestore"));
        services.ShouldBeEmpty();
    }

    /// <summary>Telemetry registration reuses the canonical SDK singleton and never grants disposition rights.</summary>
    [Fact]
    public void TelemetryRegistrationIsIdempotentAndDoesNotGrantAuthority()
    {
        ServiceCollection services = new();
        services.AddHealthChecks();
        services.AddEventStoreDomainTelemetry("work");
        services.AddWorksRecoveryTelemetry();
        ServiceDescriptor[] registered = [.. services];
        // Platform's hosting startup registers first; the parity fixture's later call adds nothing.
        services.AddWorksRecoveryTelemetry();
        services.ShouldBe(registered);
        services.Count(descriptor => descriptor.ServiceType == typeof(WorksRecoveryTelemetryPublisher)).ShouldBe(1);
        using ServiceProvider provider = services.BuildServiceProvider();
        var publishers = provider.GetServices<IHealthCheckPublisher>().ToArray();
        publishers.Length.ShouldBe(1);
        publishers[0].ShouldBeSameAs(provider.GetRequiredService<WorksRecoveryTelemetryPublisher>());
        provider.GetService<IProcessDispositionAuthorizer>().ShouldBeNull();
        provider.GetService<IProcessDispositionAuditSink>().ShouldBeNull();
    }

    private static HealthReport Report(HealthStatus status, Dictionary<string, object> data)
        => new(new Dictionary<string, HealthReportEntry> { ["eventstore-processes-unresolved"] = new(status, "SDK recovery", TimeSpan.Zero, null, data) }, TimeSpan.Zero);

    private static ClaimsPrincipal Principal() => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, "workload:eventstore-admin"),
        new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore-admin"),
        new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, "process:dispose"),
        new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, _source.Tenant),
        new Claim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, _source.Domain),
        new Claim(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "aggregate", _source.Aggregate),
        new Claim(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "purpose", "works.process-disposition"),
        new Claim(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType, "fixture-operator"),
    ], EventStoreWorkloadAuthenticationDefaults.WorkloadScheme));
}
