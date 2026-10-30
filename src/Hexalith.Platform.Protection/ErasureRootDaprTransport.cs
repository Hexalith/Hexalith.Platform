using Dapr.Client;
using Grpc.Net.Client;

namespace Hexalith.Platform.Protection;

/// <summary>Verified source-index transport: new index writes stay under the sidecar's default 4 MiB receive bound; prior base64-framed 4 MiB chunks require verified 8 MiB sidecar and SDK response allowances.</summary>
public static class ErasureRootDaprTransport
{
    /// <summary>Configures exact bounded source-state transport, retaining SDK authentication and endpoint configuration.</summary>
    public static DaprClientBuilder Configure(DaprClientBuilder builder) => builder.UseGrpcChannelOptions(new GrpcChannelOptions
    { MaxReceiveMessageSize = 8 * 1024 * 1024 });
}
