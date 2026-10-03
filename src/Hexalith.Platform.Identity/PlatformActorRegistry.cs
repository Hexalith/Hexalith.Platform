using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Platform facade over the one private EventStore global registry namespace.</summary>
public sealed class PlatformActorRegistry(IActorProxyFactory proxyFactory,
    [FromKeyedServices("identity-registry")] IIdentityAdmissionSigner registrySigner,
    IOptions<PlatformIdentityOptions> options, TimeProvider timeProvider)
{
    /// <summary>Reads already enrolled opaque capability evidence; never enrolls from a query.</summary>
    public Task<ActorRegistryEntry?> ReadAsync(string lookup, bool byAlias, string sourceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string messageId = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { lookup, byAlias });
        string proof = Sign("RegistryRead", messageId, sourceId, null, payload);
        return Proxy().ReadAsync(lookup, byAlias, messageId, proof).WaitAsync(cancellationToken);
    }

    /// <summary>Applies a verified continuity/enrollment mutation with operator attribution.</summary>
    public Task<ActorRegistryEntry> MutateAsync(ActorRegistryMutation mutation, string sourceId,
        string operatorActorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        if (mutation.RegistryNamespace != IdentityActorRegistryActor.RegistryNamespace)
        {
            throw new InvalidOperationException("Registry mutation scope is invalid.");
        }

        string proof = Sign("RegistryMutation", mutation.OperationId, sourceId, operatorActorId,
            JsonSerializer.SerializeToUtf8Bytes(mutation));
        return Proxy().MutateAsync(mutation, proof).WaitAsync(cancellationToken);
    }

    private string Sign(string operation, string messageId, string sourceId, string? operatorActorId, byte[] payload)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var scope = new IdentityAdmissionScope("identity-authority", "identity-registry", IdentityActorRegistryActor.RegistryNamespace,
            operation, messageId, messageId, IdentityAdmissionProof.Digest(payload));
        return registrySigner.Sign(new(scope, options.Value.RegistryServiceSourceId
                ?? throw new InvalidOperationException("Registry service source is unavailable."), operatorActorId, null, 0, false,
            now, now.AddMinutes(1), options.Value.AuthorityRevision));
    }

    private IIdentityActorRegistryActor Proxy()
        => proxyFactory.CreateActorProxy<IIdentityActorRegistryActor>(new ActorId(IdentityActorRegistryActor.RegistryNamespace), IdentityActorRegistryActor.ActorTypeName);
}
