using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Platform.Protection;

// AuthoritativeEventStreamReader's ReadStreamAsync is bound exclusively to the existing private workload/native read here.
internal sealed class QualificationWorkloadReadGateway(IEventStoreGatewayClient privateGateway):IEventStoreGatewayClient
{
    public Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request,CancellationToken token=default)=>privateGateway.ReadWorkloadStreamAsync(request,token);
    public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request,CancellationToken token=default)=>Task.FromException<SubmitCommandResponse>(new InvalidOperationException("Qualification audit is read-only."));
    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request,string? ifNoneMatch=null,CancellationToken cancellationToken=default)=>privateGateway.SubmitQueryAsync(request,ifNoneMatch,cancellationToken);
    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request,string? ifNoneMatch=null,CancellationToken cancellationToken=default)=>privateGateway.SubmitQueryAsync<T>(request,ifNoneMatch,cancellationToken);
}
