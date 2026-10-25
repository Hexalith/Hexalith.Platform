using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Canary driver for the actual occurrence-authenticated EventStore engine and complete original custody manifest.</summary>
public sealed class EventStoreFr34CanaryEngine(InteractionPayloadProtectionService engine, Fr34CanaryArchive writer,
    CustodyLifecycleClient custody, ICustodyReservedManifestSource manifests) : IFr34ProtectionCanaryEngine
{
    internal bool IsActualTarget(Fr34ProtectionTarget target)
    {
        var identity = engine.GetIdentity();
        return identity.Name == target.EngineId && identity.SourceRevision == target.EngineVersion && identity.SourceRevision.Length == 40;
    }
    /// <inheritdoc />
    public async Task<Fr34CanaryReference?> SealAndPersistAsync(Fr34ProtectionTarget target, string canaryId, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        if (!IsActualTarget(target) || canaryId.Length != 64 || !canaryId.All(char.IsAsciiHexDigit) || plaintext.Length is 0 or > 8192) return null;
        var owner = new AggregateIdentity(target.TenantId, "platform-fr34", canaryId);
        var keyTarget = new ProtectionTarget(target.TenantId, canaryId, "fr34-" + canaryId);
        var occurrence = new PayloadProtectionOccurrenceContext(1, PayloadProtectionPayloadKind.Event, typeof(CanaryEvent).FullName!);
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Value = Convert.ToBase64String(plaintext), Target = keyTarget } });
        PayloadProtectionCompletionContext? completion = null; bool published = false;
        try
        {
            var result = await engine.ProtectEventPayloadAsync(owner, new CanaryEvent(), occurrence.PayloadTypeId, input, "json", occurrence, cancellationToken).ConfigureAwait(false);
            completion = result.CompletionContext;
            if (completion is null || result.ProtectionResult.Metadata.State != PayloadProtectionState.Protected
                || result.ProtectionResult.Metadata.KeyAlias != completion.KeyReference || result.ProtectionResult.PayloadBytes.AsSpan().IndexOf(input) >= 0) return null;
            var reference = new Fr34CanaryReference(target, canaryId, owner.ActorId, completion.KeyReference);
            await engine.AcquirePayloadProtectionCompletionLeaseAsync(completion, cancellationToken).ConfigureAwait(false);
            await writer.PublishAsync(new(reference, owner, keyTarget, occurrence, result.ProtectionResult), cancellationToken).ConfigureAwait(false);
            published = true;
            await engine.CompletePayloadProtectionAsync(completion, PayloadProtectionPersistenceOutcome.Persisted, cancellationToken).ConfigureAwait(false);
            return reference;
        }
        catch
        {
            if (completion is not null)
                await engine.CompletePayloadProtectionAsync(completion, PayloadProtectionPersistenceOutcome.Unknown, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(input); _ = published; }
    }
    /// <inheritdoc />
    public async Task<byte[]?> UnsealAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
    {
        var outcome = await ReplayAsync(reference, cancellationToken).ConfigureAwait(false);
        if (outcome?.PayloadBytes is not { } bytes) return null;
        try { using var value = JsonDocument.Parse(bytes); return Convert.FromBase64String(value.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetString()!); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    /// <inheritdoc />
    public async Task<bool> DestroyAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
    {
        if (!IsActualTarget(reference.Target) || await writer.ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false) is not { } stored) return false;
        // The independent control owner publishes this complete signed/reserved original; a locator is never authority.
        string reservation = "fr34:" + reference.CanaryId;
        var manifest = await manifests.FindAsync(reservation, cancellationToken).ConfigureAwait(false);
        if (manifest is null || manifest.Targets.Count != 1 || manifest.Targets[0] != stored.ProtectionTarget) return false;
        var original = await custody.ExecuteManifestOriginalAsync(manifest, reservation, false, cancellationToken).ConfigureAwait(false);
        return original.Result.State == DeletionManifestProviderState.Consumed && original.RestoreBarrierReceiptId is not null
            && original.Result.TargetReceipts.Count == 1 && original.Result.TargetReceipts[0].Target == stored.ProtectionTarget;
    }
    /// <inheritdoc />
    public async Task<PayloadUnprotectionOutcome?> ReplayAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
    {
        if (!IsActualTarget(reference.Target) || await writer.ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false) is not { } original) return null;
        return await engine.TryUnprotectEventPayloadAsync(original.Owner, original.Occurrence.PayloadTypeId, original.Protected.PayloadBytes,
            original.Protected.SerializationFormat, original.Protected.Metadata, original.Occurrence, cancellationToken).ConfigureAwait(false);
    }
    private sealed record CanaryEvent : IEventPayload;
}
