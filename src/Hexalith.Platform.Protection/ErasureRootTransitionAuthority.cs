using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>ERC-backed admitted-original journal for one independently enrolled owner scope. Local absence cannot create genesis or permission.</summary>
public sealed class ErasureRootTransitionAuthority(ErasureRootCustody custody, IErasureRootAuthorization authorization,
    string tenantId, string scopeId) : IAnchoredStateTransitionAuthority
{
    /// <summary>Gets the exact independently enrolled tenant.</summary>
    public string TenantId { get; } = tenantId;
    /// <summary>Gets the exact independently enrolled owner purpose.</summary>
    public string ScopeId { get; } = scopeId;

    /// <summary>Verifies fresh independently surviving revision/digest, including only explicitly installed genesis.</summary>
    public async Task<bool> ValidateStateAsync(long revision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        try
        {
            if (revision < 0 || !Digest(exactStateDigest)
                || !await authorization.AuthorizeAsync(TenantId, ScopeId, "ValidateOwnerState", ErasureRootCustody.Hash(new { revision, exactStateDigest }), cancellationToken).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(TenantId, cancellationToken).ConfigureAwait(false);
            return read is { } current && current.Head.Anchors.TryGetValue(ScopeId, out var anchor)
                && anchor.Revision == revision && anchor.Digest == exactStateDigest;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }

    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ApplyAsync(transition, "AdmitTransition", cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ApplyAsync(transition, "RecordTransition", cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ApplyAsync(transition, "RecoverTransition", cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => ApplyAsync(transition, "VerifyTransition", cancellationToken);

    private async Task<bool> ApplyAsync(AnchoredStateTransition supplied, string operation, CancellationToken token)
    {
        try
        {
            if (supplied is null || supplied.ScopeId != ScopeId || supplied.ExpectedRevision < 0 || supplied.ExpectedRevision == long.MaxValue
                || supplied.TargetRevision != supplied.ExpectedRevision + 1 || !Digest(supplied.PredecessorDigest) || !Digest(supplied.TargetDigest)
                || supplied.TargetBytes is not { Length: > 0 and <= 8388608 }
                || Convert.ToHexString(SHA256.HashData(supplied.TargetBytes)) != supplied.TargetDigest) { return false; }
            var transition = supplied with { TargetBytes = supplied.TargetBytes.ToArray() };
            string id = ErasureRootCustody.Hash(transition);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.VerifyTransitionPermissionAsync(transition, operation, token).ConfigureAwait(false)) { return false; }
                var read = await custody.ReadAsync(TenantId, token).ConfigureAwait(false);
                if (read is not { } current || !current.Head.Anchors.TryGetValue(ScopeId, out var anchor)) { return false; }
                if (anchor.Completed.TryGetValue(id, out var completed)) { return ErasureRootCustody.Hash(completed) == id; }
                if (operation == "VerifyTransition" || current.Head.Pending is not null) { return false; }
                if (anchor.Revision != transition.ExpectedRevision || anchor.Digest != transition.PredecessorDigest) { return false; }
                if (operation == "AdmitTransition")
                {
                    if (anchor.Admissions.TryGetValue(id, out var admitted)) { return ErasureRootCustody.Hash(admitted) == id; }
                    anchor.Admissions.Add(id, transition);
                }
                else
                {
                    if (!anchor.Admissions.TryGetValue(id, out var admitted) || ErasureRootCustody.Hash(admitted) != id) { return false; }
                    anchor.Completed.Add(id, transition);
                    current.Head.Anchors[ScopeId] = anchor with { Revision = transition.TargetRevision, Digest = transition.TargetDigest };
                }
                if (!await authorization.VerifyTransitionPermissionAsync(transition, operation, token).ConfigureAwait(false)) { return false; }
                if (await custody.SaveAsync(TenantId, current.Version, current.Head, token).ConfigureAwait(false)) { return true; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { token.ThrowIfCancellationRequested(); }
        return false;
    }

    private static bool Digest(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
