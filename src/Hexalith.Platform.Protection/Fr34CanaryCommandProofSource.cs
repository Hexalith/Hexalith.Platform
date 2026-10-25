using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Dedicated exact canary workload proof, privately captured by FR-34. It cannot submit another stream or event.</summary>
public sealed class Fr34CanaryCommandProofSource(string caller, string principal, string mountedRsaKeyFile,
    IndependentDaprAuthority currentScopes, TimeProvider clock) : IEventStoreGatewayBoundCommandProofSource
{
    /// <inheritdoc />
    public async ValueTask<string?> IssueAsync(BoundWorkloadCommandRequest request, CancellationToken cancellationToken)
    {
        var command = request.Command;
        if (request.PrincipalId != principal || command.Domain != "fr34-canary" || command.CommandType != typeof(PersistFr34Canary).FullName
            || command.Extensions is { Count: > 0 } || command.MessageId != command.IdempotencyKey
            || await currentScopes.InvokeAsync<Permission>("fr34-command-authorize", new { caller, principal, command }, cancellationToken).ConfigureAwait(false) is not { Allowed: true }) return null;
        var info = new FileInfo(mountedRsaKeyFile);
        if (!info.Exists || info.LinkTarget is not null || info.Length is <= 0 or > 16384) return null;
        string key = (await File.ReadAllTextAsync(mountedRsaKeyFile, cancellationToken).ConfigureAwait(false)).Trim();
        return BoundWorkloadCommandProof.Issue(request, caller, key, clock.GetUtcNow());
    }
    private sealed record Permission(bool Allowed);
}
