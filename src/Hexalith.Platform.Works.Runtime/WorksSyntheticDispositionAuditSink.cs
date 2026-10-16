using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Platform.Works.Runtime;

/// <summary>Persists bounded attributed qualification audit through the SDK's durable CAS store.</summary>
public sealed class WorksSyntheticDispositionAuditSink : IProcessDispositionAuditSink
{
    private readonly IReadModelStore _store;
    private readonly WorksSyntheticDispositionAuthorizer _authorizer;
    private readonly string _storeName;
    private readonly TimeProvider _time;

    /// <summary>Creates a sink for the explicitly selected disposable state store.</summary>
    /// <param name="store">SDK durable store; failures propagate to stop disposition.</param>
    /// <param name="authorizer">Exact source and signed operator grant validator.</param>
    /// <param name="storeName">State component included in complete-fleet backup.</param>
    /// <param name="time">UTC time source.</param>
    public WorksSyntheticDispositionAuditSink(IReadModelStore store, WorksSyntheticDispositionAuthorizer authorizer, string storeName, TimeProvider time)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        _storeName = storeName;
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Returns the stable, identity-hiding source key used by the full-store restore proof.</summary>
    /// <param name="source">The canonical process source.</param>
    /// <returns>A bounded key in the Works app namespace.</returns>
    public static string Key(ProcessSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        return "platform:works:process-disposition-audit:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.CatalogKey)));
    }

    /// <inheritdoc/>
    public async Task RecordAsync(ClaimsPrincipal principal, ProcessSource source, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 256) throw new ArgumentException("Disposition reason exceeds the endpoint limit.", nameof(reason));
        if (!await _authorizer.AuthorizeAsync(principal, source, cancellationToken).ConfigureAwait(false))
            throw new UnauthorizedAccessException("Synthetic disposition audit refused an unbound operator/source.");
        ClaimsIdentity identity = principal.Identities.Single(value => value.IsAuthenticated);
        var attempt = new WorksProcessDispositionAuditRecord(
            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), _time.GetUtcNow(),
            identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)!.Value,
            identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType)!.Value,
            source, "works.process-disposition", reason);
        _ = await ReadModelWritePolicy.UpdateAsync<WorksProcessDispositionAuditDocument>(_store, _storeName, Key(source), prior =>
        {
            IReadOnlyList<WorksProcessDispositionAuditRecord> attempts = prior?.Attempts ?? [];
            if (attempts.Any(value => value.AttemptId == attempt.AttemptId)) return prior!;
            if (attempts.Count >= 256) throw new InvalidOperationException("Synthetic disposition audit capacity reached; mutation is refused.");
            return new WorksProcessDispositionAuditDocument([.. attempts, attempt]);
        }, maxAttempts: 8, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
