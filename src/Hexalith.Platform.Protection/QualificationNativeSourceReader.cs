using System.Security.Cryptography;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Platform.Protection;

/// <summary>Actual EventStore source reader for replay, denial, canary and guard-related source writes. It never accepts owner receipts or returns payload content.</summary>
public sealed class QualificationNativeSourceReader(IEventStoreGatewayClient privateReadGateway,TimeProvider clock)
{
    private readonly AuthoritativeEventStreamReader _reader=new(privateReadGateway,clock,requireNativeProof:true);
    /// <summary>Reads each independently enrolled exact identity and measures actual persisted bytes, preserving native incarnation/head evidence.</summary>
    public async Task<IReadOnlyList<QualificationNativeStream>?> ReadAsync(IReadOnlyList<AggregateIdentity> enrolledTargets,CancellationToken token)
    {
        if(enrolledTargets.Count is 0 or >32 || enrolledTargets.Distinct().Count()!=enrolledTargets.Count) return null;
        var observations=new List<QualificationNativeStream>();
        foreach(var identity in enrolledTargets)
        {
            var read=await _reader.ReadAsync(identity,token).ConfigureAwait(false);
            if(!read.IsAuthoritative || read.Stream?.NativeProof is not { CodecVersion:1,Erased:false,RetainedFloor:1 } proof || read.Stream.Events.Count>10000) return null;
            var records=new List<QualificationNativeEvent>();
            foreach(var item in read.Stream.Events)
            {
                if(item.NativeEventDigest is not { Length:64 } nativeDigest) return null;
                records.Add(new(item.SequenceNumber,item.MessageId,item.EventTypeName,Convert.ToHexStringLower(SHA256.HashData(item.Payload)),
                    nativeDigest,item.ProtectionMetadata?.Scheme,item.ProtectionMetadata?.KeyAlias));
            }
            observations.Add(new(identity,proof,records));
        }
        return observations;
    }
}
