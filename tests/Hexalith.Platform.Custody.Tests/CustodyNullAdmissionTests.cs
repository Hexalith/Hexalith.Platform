using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Preserves null refusal and caller cancellation at the current custody build boundaries.</summary>
public sealed class CustodyNullAdmissionTests
{
    /// <summary>Missing envelopes release no material and retain caller cancellation on both entry points.</summary>
    /// <param name="admission">Whether to exercise the final admission entry point.</param>
    /// <param name="cancelled">Whether the original caller has cancelled.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MissingEnvelopesPreserveRefusalAndCancellation(bool admission, bool cancelled)
    {
        var keys = Substitute.For<IPlatformHmacKeyProvider>();
        var profiles = Substitute.For<IPlatformSigningProfileProvider>();
        var registrar = Substitute.For<ITrustedEnvelopeReplayRegistrar>();
        var authenticator = new TrustedEnvelopeAuthenticator(keys, profiles, TimeProvider.System);
        var verifier = new TrustedEnvelopeReplayVerifier(authenticator, registrar, profiles, TimeProvider.System);
        var expected = new TrustedEnvelopeIdentity(1, "fixture-r1", "fixture-issuer",
            new(TrustedPrincipalKind.User, "tenant-a", "actor-a", "party-a", 1, "current-roles", null, null, null, null),
            "Contracts.EditProposal", "EditProposal", "tenant-a", "interaction-a", "correlation", "cause",
            ["tenant-a", "User", "actor-a", "EditProposal", "client-key"], "fingerprint", "digest-v1", "fixture-audience", "logical-command");
        using var caller = new CancellationTokenSource();
        if (cancelled) caller.Cancel();
        bool readinessCalled = false;
        Task<TrustedEnvelopeResult> ReadAsync() => admission
            ? verifier.VerifyForAdmissionAsync(null!, expected, _ =>
            {
                readinessCalled = true;
                return Task.FromResult(true);
            }, caller.Token)
            : verifier.VerifyAsync(null!, expected, caller.Token);

        if (cancelled)
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(ReadAsync).ConfigureAwait(true);
            error.CancellationToken.ShouldBe(caller.Token);
        }
        else
        {
            TrustedEnvelopeResult result = await ReadAsync().ConfigureAwait(true);
            result.Status.ShouldBe(CustodyStatus.Unavailable);
            result.Envelope.ShouldBeNull();
        }

        readinessCalled.ShouldBeFalse();
        keys.ReceivedCalls().ShouldBeEmpty();
        profiles.ReceivedCalls().ShouldBeEmpty();
        registrar.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Invalid export input retains the exact protocol refusal without accessing private providers.</summary>
    /// <param name="caseId">The selected export protocol.</param>
    /// <param name="missing">The existing missing-protocol diagnostic.</param>
    [Theory]
    [InlineData("S3.aes256gcm", "ExactValidExportManifestAndContext")]
    [InlineData("unsupported", "SupportedExportQualificationCase")]
    public async Task MissingExportInputPreservesPreflightRefusal(string caseId, string missing)
    {
        var materials = Substitute.For<IExportQualificationMaterialSource>();
        var trust = Substitute.For<IExportQualificationTrustSource>();
        var engine = new ExportQualificationEngine(materials, trust);

        (await engine.PreflightAsync(caseId, null!, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBe([missing]);
        materials.ReceivedCalls().ShouldBeEmpty();
        trust.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A missing raw receiving identity is refused before any fields are captured.</summary>
    [Fact]
    public void MissingRawIdentityIsRefused()
        => Should.Throw<ArgumentNullException>(() => ExportKeyDeliveryRawIdentity.Capture(null!)).ParamName.ShouldBe("value");
}
