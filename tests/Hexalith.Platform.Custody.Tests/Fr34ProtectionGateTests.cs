using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Executable gate observations with synthetic independent target/storage/destruction ports and real byte encryption; no production qualification is inferred.</summary>
public sealed class Fr34ProtectionGateTests
{
    /// <summary>No-op/wrapper, target substitution, inconsistent bytes, nonterminal destruction and readable replay fail the full gate.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("no-op", false)]
    [InlineData("wrong-target", false)]
    [InlineData("foreign-canary", false)]
    [InlineData("malformed-metadata", false)]
    [InlineData("wrong-unseal", false)]
    [InlineData("missing-destruction", false)]
    [InlineData("readable-replay", false)]
    [InlineData("wrong-erasure-kind", false)]
    [InlineData("expiry", false)]
    [InlineData("authority-change", false)]
    public async Task FreshFullObservationRequiresEveryIndependentExactProof(string vector, bool expected)
    {
        var fixture = Arrange(vector);
        (await fixture.Gate.EvaluateAsync(TestContext.Current.CancellationToken)).ShouldBe(expected);
        if (vector == "no-op") { await fixture.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken); }
        if (vector is "wrong-target" or "foreign-canary") { await fixture.Engine.DidNotReceiveWithAnyArgs().DestroyAsync(default!, TestContext.Current.CancellationToken); }
    }

    /// <summary>Startup, every readiness evaluation and content admission generate distinct canaries; later failure is never cached away.</summary>
    [Fact]
    public async Task StartupEveryReadinessAndContentAdmissionRunFreshCanaries()
    {
        var fixture = Arrange("valid"); var canaries = new HashSet<string>();
        fixture.Engine.When(engine => engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()))
            .Do(call => canaries.Add(call.ArgAt<string>(1)).ShouldBeTrue());
        await new Fr34ProtectionStartupGate(fixture.Gate).StartAsync(TestContext.Current.CancellationToken);
        var readiness = new Fr34ProtectionReadinessCheck(fixture.Gate);
        (await readiness.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Healthy);
        (await readiness.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Healthy);
        await fixture.Gate.RequireForContentAsync(TestContext.Current.CancellationToken);
        canaries.Count.ShouldBe(4);
        fixture.Authority.ObserveAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<CancellationToken>()).Returns((Fr34CanaryAuthorization?)null);
        (await readiness.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Unhealthy);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Gate.RequireForContentAsync(TestContext.Current.CancellationToken));
        canaries.Count.ShouldBe(4);
    }

    /// <summary>A caller cancellation stops a suspended canary dependency promptly and never releases readiness.</summary>
    [Fact]
    public async Task CancelledNoncooperativeSealCannotReleaseReadiness()
    {
        var fixture = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<Fr34CanaryReference?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => { entered.TrySetResult(); return pending.Task; });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); var operation = fixture.Gate.EvaluateAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        pending.SetResult(null);
        await fixture.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>A seal completing after caller cancellation cleans up only its original owned canary and cannot resume the gate.</summary>
    [Fact]
    public async Task LateOwnedSealIsCleanedWithoutResumingReadiness()
    {
        var fixture = Arrange("valid"); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var destroyed = new TaskCompletionSource<Fr34CanaryReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<Fr34CanaryReference?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Fr34CanaryReference? original = null;
        fixture.Engine.SealAndPersistAsync(Arg.Any<Fr34ProtectionTarget>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => { original = new(call.Arg<Fr34ProtectionTarget>(), call.Arg<string>(), "owned-record", "owned-key"); entered.TrySetResult(); return pending.Task; });
        fixture.Engine.DestroyAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>())
            .Returns(call => { destroyed.TrySetResult(call.Arg<Fr34CanaryReference>()); return true; });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var operation = fixture.Gate.EvaluateAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        pending.SetResult(original);
        (await destroyed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe(original);
        await fixture.Engine.DidNotReceiveWithAnyArgs().UnsealAsync(default!, TestContext.Current.CancellationToken);
    }

    private static (Fr34ProtectionGate Gate, IFr34ProtectionCanaryEngine Engine, IFr34CanaryAuthority Authority) Arrange(string vector)
    {
        var clock = new CustodyFixtureClock(); var target = new Fr34ProtectionTarget("engine", "immutable-v1", "custody-a", "canary-tenant");
        var engine = Substitute.For<IFr34ProtectionCanaryEngine>(); var storage = Substitute.For<IFr34PersistedCanaryReader>();
        var authority = Substitute.For<IFr34CanaryAuthority>();
        var authorization = new Fr34CanaryAuthorization(target, "current-authority", clock.Now, clock.Now.AddMinutes(1));
        int observations = 0;
        authority.ObserveAsync(target, Arg.Any<CancellationToken>()).Returns(_ =>
            vector == "authority-change" && ++observations > 1 ? authorization with { AuthorityRevision = "changed-authority" } : authorization);
        byte[]? plaintext = null; byte[]? sealedBytes = null;
        engine.SealAndPersistAsync(target, Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            plaintext = call.ArgAt<byte[]>(2).ToArray();
            byte[] key = RandomNumberGenerator.GetBytes(32), nonce = RandomNumberGenerator.GetBytes(12), tag = new byte[16], cipher = new byte[plaintext.Length];
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plaintext, cipher, tag);
            sealedBytes = vector == "no-op" ? plaintext.ToArray() : nonce.Concat(cipher).Concat(tag).ToArray();
            CryptographicOperations.ZeroMemory(key);
            return new Fr34CanaryReference(vector == "wrong-target" ? target with { EngineVersion = "uncommitted-version" } : target,
                vector == "foreign-canary" ? "foreign-canary" : call.ArgAt<string>(1), "persisted-canary-" + call.ArgAt<string>(1), "dedicated-canary-dek");
        });
        storage.ReadAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(call =>
            new Fr34PersistedCanary(call.Arg<Fr34CanaryReference>(), sealedBytes!.ToArray(), new(PayloadProtectionState.Protected, 1,
                vector == "malformed-metadata" ? "malformed\ncarrier" : "actual-fixture", "key-v1", null, null)));
        engine.UnsealAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(_ =>
            vector == "wrong-unseal" ? new byte[plaintext!.Length] : plaintext!.ToArray());
        engine.DestroyAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.ConfirmDestroyedAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(vector != "missing-destruction");
        engine.ReplayAsync(Arg.Any<Fr34CanaryReference>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (vector == "expiry") { clock.Now = authorization.ValidUntil; }
            return vector == "readable-replay" ? PayloadUnprotectionOutcome.Readable(plaintext!.ToArray(), "json", EventStorePayloadProtectionMetadata.Unprotected())
                : PayloadUnprotectionOutcome.Unreadable(vector == "wrong-erasure-kind" ? UnreadableProtectedDataReason.MissingKey : UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        });
        return (new(target, engine, storage, authority, clock), engine, authority);
    }
}
