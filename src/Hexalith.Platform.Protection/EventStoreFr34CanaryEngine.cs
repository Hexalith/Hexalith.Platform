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
public sealed class EventStoreFr34CanaryEngine(InteractionPayloadProtectionService engine, Fr34EventStoreCanaryReader reader,
    Hexalith.EventStore.Client.Gateway.IEventStoreGatewayClient privateWriteGateway,
    Hexalith.EventStore.Client.Gateway.IEventStoreGatewayBoundCommandProofSource canaryProofs,
    IndependentDaprAuthority originals, string privatePrincipal, CustodyLifecycleClient custody, ICustodyReservedManifestSource manifests,IFr34OccurrenceEnrollment? dynamicEnrollment=null,
    IReadOnlyDictionary<string,Fr34NativeManifestBinding>? installedManifests=null) : IFr34ProtectionCanaryEngine
{
    /// <summary>Reads identity from the actual production engine, never the requested target.</summary>
    public Hexalith.EventStore.PayloadProtection.InteractionProtectionEngineIdentity ReadEngineIdentity()=>engine.GetIdentity();
    internal bool IsActualTarget(Fr34ProtectionTarget target)
    {
        var identity = engine.GetIdentity();
        return identity.Name == target.EngineId && identity.SourceRevision == target.EngineVersion && identity.SourceRevision.Length == 40;
    }
    /// <inheritdoc />
    public async Task<Fr34CanaryReference?> SealAndPersistAsync(Fr34ProtectionTarget target, string canaryId, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        if (!IsActualTarget(target) || canaryId.Length != 64 || !canaryId.All(char.IsAsciiHexDigit) || plaintext.Length is 0 or > 8192) return null;
        if (await reader.ReadCanaryAsync(target, canaryId, cancellationToken).ConfigureAwait(false) is { } existing) return existing.Reference;
        var protectionTarget=new ProtectionTarget(target.TenantId,canaryId,"fr34-"+canaryId);
        if(dynamicEnrollment is not null)
        {
            var claim=await dynamicEnrollment.ResolveAsync(new(target.TenantId,"fr34-canary",canaryId),new(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!),cancellationToken).ConfigureAwait(false);
            if(claim is null || claim.CanaryId!=canaryId) return null;protectionTarget=claim.Binding.Identity.Target;
        }
        var payload = new PersistFr34Canary(new(Convert.ToBase64String(plaintext),protectionTarget));
        string id = EventStorePrivateRecordingPort.StableUlid("fr34", target.TenantId, canaryId);
        var command = new Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest(id,target.TenantId,"fr34-canary",canaryId,
            typeof(PersistFr34Canary).FullName!,JsonSerializer.SerializeToElement(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web)),IdempotencyKey:id);
        var original = await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=id },cancellationToken).ConfigureAwait(false);
        if (original is null)
        {
            if (await originals.InvokeAsync<Admission>("qualification-begin-original",new { role="fr34-engine",requestId=id,original=command.Payload },cancellationToken).ConfigureAwait(false) is not { Exact:true }) return null;
            original = await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=id },cancellationToken).ConfigureAwait(false);
        }
        if (original?.Request.GetRawText() != command.Payload.GetRawText() || original.SubmissionState != "Unsent") return null;
        var request = new Hexalith.EventStore.Contracts.Commands.BoundWorkloadCommandRequest(command,privatePrincipal);
        string? proof = await canaryProofs.IssueAsync(request,cancellationToken).ConfigureAwait(false);
        if (proof is null || await originals.InvokeAsync<Permission>("recording-dispatch-admit",new { role="fr34-engine",requestId=id,original=command.Payload },cancellationToken).ConfigureAwait(false) is not { Allowed:true }) return null;
        _ = await privateWriteGateway.SubmitBoundWorkloadCommandAsync(request,proof,cancellationToken).ConfigureAwait(false);
        return (await reader.ReadCanaryAsync(target,canaryId,cancellationToken).ConfigureAwait(false))?.Reference;
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
        if (!IsActualTarget(reference.Target) || await reader.ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false) is not { } stored) return false;
        // The independent control owner publishes this complete signed/reserved original; a locator is never authority.
        Fr34NativeManifestBinding? expected=null;
        if(dynamicEnrollment is not null)
        {
            var claim=await dynamicEnrollment.ResolveAsync(stored.Owner,stored.Occurrence,cancellationToken).ConfigureAwait(false);
            if(claim?.NativeManifest is not { } native || claim.Binding.Identity.Target!=stored.ProtectionTarget || claim.Binding.KeyReference!=reference.KeyReference) return false;
            expected=native;
        }
        else if(installedManifests?.TryGetValue(reference.RecordId,out expected)!=true) return false;
        if(expected is null) return false;string reservation=expected.ReservationReceiptId;
        var manifest = await manifests.FindAsync(reservation, cancellationToken).ConfigureAwait(false);
        if (manifest is null || manifest.Targets.Count != 1 || manifest.Targets[0] != stored.ProtectionTarget
            || JsonSerializer.Serialize(manifest,CustodyBindingContract.Json)!=JsonSerializer.Serialize(expected.Original,CustodyBindingContract.Json)) return false;
        var original = await custody.ExecuteManifestOriginalAsync(manifest, reservation, false, cancellationToken).ConfigureAwait(false);
        return original.Result.State == DeletionManifestProviderState.Consumed && original.RestoreBarrierReceiptId is not null
            && original.Result.TargetReceipts.Count == 1 && original.Result.TargetReceipts[0].Target == stored.ProtectionTarget;
    }
    /// <inheritdoc />
    public async Task<PayloadUnprotectionOutcome?> ReplayAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
    {
        if (!IsActualTarget(reference.Target) || await reader.ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false) is not { } original) return null;
        return await engine.TryUnprotectEventPayloadAsync(original.Owner, original.Occurrence.PayloadTypeId, original.Protected.PayloadBytes,
            original.Protected.SerializationFormat, original.Protected.Metadata, original.Occurrence, cancellationToken).ConfigureAwait(false);
    }
    private sealed record Admission(bool Exact,bool First);
    private sealed record Permission(bool Allowed);
}
