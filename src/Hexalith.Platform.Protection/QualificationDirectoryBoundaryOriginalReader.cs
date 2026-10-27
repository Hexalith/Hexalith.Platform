using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete current directory epoch/repair/drain/activation ledger reader with exact original lookup and stable-read verification. It starts no installation, migration or target write.</summary>
public sealed class QualificationDirectoryBoundaryOriginalReader(Func<string,IDirectoryMigrationBoundaryActor>? actors=null)
{
    /// <summary>Reads all actual retained boundary outcomes and predecessor facts; unselected writer-native enforcement/proof APIs remain separate.</summary>
    public async Task<QualificationDirectoryBoundaryOriginals?> ReadAsync(string tenant,CancellationToken token)
    {
        var actor=actors?.Invoke(tenant)??ActorProxy.Create<IDirectoryMigrationBoundaryActor>(new ActorId(DirectoryMigrationBoundaryActor.GetActorId(tenant)),DirectoryMigrationBoundaryActor.ActorTypeName);
        var ledger=await actor.ReadAsync(tenant).WaitAsync(token).ConfigureAwait(false);
        if(ledger is null || ledger.TenantId!=tenant || ledger.Revision<1 || ledger.Outcomes.Count>10000 || ledger.Outcomes.Select(value=>value.OperationId).Distinct(StringComparer.Ordinal).Count()!=ledger.Outcomes.Count) return null;
        var originals=new List<DirectoryBoundaryOutcome>();
        foreach(var outcome in ledger.Outcomes)
        {
            var original=await actor.LookupAsync(tenant,outcome.OperationId,outcome.RequestDigest).WaitAsync(token).ConfigureAwait(false);
            if(original!=outcome || outcome.CommittedRevision<=0 || outcome.CommittedRevision>ledger.Revision) return null;originals.Add(original);
        }
        var after=await actor.ReadAsync(tenant).WaitAsync(token).ConfigureAwait(false);
        return JsonSerializer.Serialize(after)==JsonSerializer.Serialize(ledger)?new(ledger,originals):null;
    }
}
