using System.Text.Json;
using Dapr.Client;
using NSubstitute;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Independent durable CAS root and content-addressed native cells, without an ERC dependency.</summary>
internal sealed class ErasureRootIndependentSourceFixture : IDisposable
{
    internal DaprClient Native { get; } = Substitute.For<DaprClient>();
    internal ErasureRootSourceOriginals Root { get; set; } = new("tenant-a", "erc-test", new());
    internal Dictionary<string, ErasureRootSourceIndexNode> Nodes { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]> Chunks { get; } = new(StringComparer.Ordinal);
    internal int Revision { get; private set; }
    internal ErasureRootIndependentSourceFixture()
    {
        Native.GetStateAndETagAsync<ErasureRootSourceOriginals>("independent", "root", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (JsonSerializer.Deserialize<ErasureRootSourceOriginals>(JsonSerializer.SerializeToUtf8Bytes(Root))!, Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Native.GetStateAsync<ErasureRootSourceOriginals>("independent", "root", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => Root);
        Native.TrySaveStateAsync("independent", "root", Arg.Any<ErasureRootSourceOriginals>(), Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { if (call.ArgAt<string>(3) != Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)) { return false; } Root = call.ArgAt<ErasureRootSourceOriginals>(2); Revision++; return true; });
        Native.GetStateAsync<ErasureRootSourceIndexNode>("independent", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => Nodes.GetValueOrDefault(call.ArgAt<string>(1))!);
        Native.SaveStateAsync("independent", Arg.Any<string>(), Arg.Any<ErasureRootSourceIndexNode>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { string key = call.ArgAt<string>(1); var node = call.ArgAt<ErasureRootSourceIndexNode>(2); if (Nodes.TryGetValue(key, out var old) && ErasureRootCustody.Hash(old) != ErasureRootCustody.Hash(node)) { throw new InvalidOperationException(); } Nodes[key] = node; return Task.CompletedTask; });
        Native.GetStateAsync<byte[]>("independent", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => Chunks.GetValueOrDefault(call.ArgAt<string>(1))!);
        Native.SaveStateAsync("independent", Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { string key = call.ArgAt<string>(1); byte[] bytes = call.ArgAt<byte[]>(2); if (Chunks.TryGetValue(key, out var old) && !old.AsSpan().SequenceEqual(bytes)) { throw new InvalidOperationException(); } Chunks[key] = bytes.ToArray(); return Task.CompletedTask; });
    }
    public void Dispose() => Native.Dispose();
}
