using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf;
using Hexalith.Platform.Secrets.Runtime;

/// <summary>Disposable gRPC state transport for the real independent receiver; no installed store is opened.</summary>
internal sealed class RegistrationAuthorityStateTransport(IndependentAuthorityState initial) : HttpMessageHandler
{
    internal IndependentAuthorityState State = initial;
    private int _etag;
    internal void Replace(IndependentAuthorityState state) { State=state;_etag++; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        byte[] frame=await request.Content!.ReadAsByteArrayAsync(token);
        IMessage result;
        if(request.RequestUri!.AbsolutePath.EndsWith("/GetState",StringComparison.Ordinal))
            result=new GetStateResponse { Data=ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(State,CustodyBindingContract.Json)),Etag=_etag.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        else if(request.RequestUri.AbsolutePath.EndsWith("/SaveState",StringComparison.Ordinal))
        {
            var item=SaveStateRequest.Parser.ParseFrom(frame.AsSpan(5).ToArray()).States.Single();
            if(item.Etag.Value!=_etag.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new InvalidOperationException("Fixture ETag conflict.");
            Replace(JsonSerializer.Deserialize<IndependentAuthorityState>(item.Value.Span,CustodyBindingContract.Json)!);
            result=new Google.Protobuf.WellKnownTypes.Empty();
        }
        else throw new InvalidOperationException("Unexpected fixture state operation.");
        byte[] protobuf=result.ToByteArray();byte[] output=new byte[protobuf.Length+5];BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(1,4),protobuf.Length);protobuf.CopyTo(output,5);
        var response=new HttpResponseMessage(HttpStatusCode.OK) { Version=new(2,0),Content=new ByteArrayContent(output) };
        response.Content.Headers.ContentType=new MediaTypeHeaderValue("application/grpc");response.TrailingHeaders.Add("grpc-status","0");return response;
    }
}
