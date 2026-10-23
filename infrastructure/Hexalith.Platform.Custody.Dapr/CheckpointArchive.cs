using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

internal sealed record CheckpointPayload(string Kind, string? Id, ObjectVersion Object);
internal sealed record CheckpointArchiveV1(int SchemaVersion, JsonElement Index, CheckpointPayload[] Payloads);
internal sealed record CheckpointArchivePlan(CheckpointArchiveV1 Archive, Dictionary<string, byte[]> NewPayloads, long MaximumBytes);

// Preparation performs no I/O. Exact predecessor references are reused; only newly introduced
// complete payloads are written before the one bounded index/head comparison.
internal static class CheckpointArchive
{
    private const int MaximumPayloads = 21001;
    private const int Parallelism = 4;
    internal static CheckpointArchivePlan Prepare(Checkpoint checkpoint, CheckpointArchiveV1? predecessor, long maximumBytes)
    {
        var index = JsonSerializer.SerializeToNode(checkpoint, CustodyBindingContract.Json)!.AsObject();
        var payloads = new List<CheckpointPayload>();
        var versions = (predecessor?.Payloads ?? []).Select(p => p.Object).Distinct()
            .GroupBy(v => v.Sha256, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var pending = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        void Extract(JsonObject parent, string property, string kind, string? id)
        {
            if (parent[property] is not { } node) return;
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(node, CustodyBindingContract.Json);
            if (bytes.Length is 0 or > CustodyBindingContract.MaximumOriginalBytes) throw new AuthorityUnavailableException("Checkpoint payload bounds exceeded.");
            string digest = CustodyJson.Hash(bytes);
            if (!versions.TryGetValue(digest, out var version))
            {
                pending.TryAdd(digest, bytes);
                // Journal keys are generated ASCII (105 chars). Native version IDs are bounded to
                // 1024 UTF-8 bytes; NUL's JSON escaping conservatively reserves their full encoding.
                version = new(new string('k', 105), new string('\0', 1024), digest, bytes.Length, DateTimeOffset.MaxValue);
            }
            else if (version.Bytes != bytes.Length) throw new AuthorityUnavailableException("Predecessor payload length mismatch.");
            payloads.Add(new(kind, id, version)); parent[property] = null;
        }
        foreach (var pair in index["Originals"]!.AsObject()) Extract(pair.Value!.AsObject(), "Manifest", "original-manifest", pair.Key);
        if (index["Cycle"] is { } cycle) Extract(cycle.AsObject(), "Manifest", "cycle-manifest", null);
        foreach (var pair in index["Anchors"]!.AsObject()) Extract(pair.Value!.AsObject(), "Admitted", "owner-admission", pair.Key);
        var registrations = index["Registrations"]!.AsObject();
        foreach (string id in registrations.Select(pair => pair.Key).ToArray()) Extract(registrations, id, "registration", id);
        var archive = new CheckpointArchiveV1(1, JsonSerializer.SerializeToElement(index), payloads.ToArray());
        if (payloads.Count > MaximumPayloads || CustodyJson.Bytes(archive).Length > maximumBytes)
            throw new AuthorityUnavailableException("Finite checkpoint index capacity exhausted before immutable writes.");
        return new(archive, pending, maximumBytes);
    }
    internal static async Task<byte[]> WriteAsync(CheckpointArchivePlan plan,
        Func<ReadOnlyMemory<byte>, string, CancellationToken, Task<ObjectVersion>> put, CancellationToken token)
    {
        using var gate = new SemaphoreSlim(Parallelism);
        var written = await Task.WhenAll(plan.NewPayloads.Select(async pair => {
            await gate.WaitAsync(token);
            try
            {
                var version = await put(pair.Value, "journal", token);
                if (version.Key.Length > 105 || string.IsNullOrWhiteSpace(version.VersionId) || Encoding.UTF8.GetByteCount(version.VersionId) > 1024
                    || version.Sha256 != pair.Key || version.Bytes != pair.Value.Length)
                    throw new AuthorityUnavailableException("Native payload reference exceeds its preflight reservation.");
                return (pair.Key, Version: version);
            }
            finally { gate.Release(); }
        }));
        var versions = written.ToDictionary(pair => pair.Key, pair => pair.Version, StringComparer.Ordinal);
        var archive = plan.Archive with { Payloads = plan.Archive.Payloads.Select(p => versions.TryGetValue(p.Object.Sha256, out var version) ? p with { Object = version } : p).ToArray() };
        byte[] bytes = CustodyJson.Bytes(archive);
        if (bytes.Length > plan.MaximumBytes) throw new AuthorityUnavailableException("Native payload reference exceeded bounded checkpoint capacity.");
        return bytes;
    }
    internal static Task<byte[]> EncodeAsync(Checkpoint checkpoint,
        Func<ReadOnlyMemory<byte>, string, CancellationToken, Task<ObjectVersion>> put, CancellationToken token)
        => WriteAsync(Prepare(checkpoint, null, 32 * 1024 * 1024), put, token);

    internal static async Task<Checkpoint> DecodeAsync(ReadOnlyMemory<byte> bytes,
        Func<ObjectVersion, CancellationToken, Task<byte[]>> read, CancellationToken token)
    {
        if (bytes.Length > 32 * 1024 * 1024) throw new AuthorityUnavailableException("Oversized checkpoint index.");
        var archive = CustodyJson.Read<CheckpointArchiveV1>(bytes.Span);
        if (archive.SchemaVersion != 1 || archive.Payloads.Length > MaximumPayloads)
            throw new AuthorityUnavailableException("Unsupported checkpoint index.");
        var index = JsonNode.Parse(archive.Index.GetRawText())!.AsObject();
        var paths = new HashSet<(string, string?)>();
        foreach (var payload in archive.Payloads)
            if (!paths.Add((payload.Kind, payload.Id))) throw new AuthorityUnavailableException("Duplicate checkpoint payload.");
        // Unique versions load at bounded concurrency, rather than a serial thousand-read chain.
        using var gate = new SemaphoreSlim(Parallelism);
        var loaded = (await Task.WhenAll(archive.Payloads.Select(p => p.Object).Distinct().Select(async version => {
            if (version.Bytes is <= 0 or > CustodyBindingContract.MaximumOriginalBytes) throw new AuthorityUnavailableException("Invalid checkpoint payload size.");
            await gate.WaitAsync(token);
            try {
                byte[] exact = await read(version, token);
                if (exact.Length != version.Bytes || CustodyJson.Hash(exact) != version.Sha256) throw new AuthorityUnavailableException("Checkpoint payload version mismatch.");
                return (Version: version, Bytes: exact);
            }
            finally { gate.Release(); }
        })) ).ToDictionary(x => x.Version, x => x.Bytes);
        foreach (var payload in archive.Payloads)
        {
            (JsonObject parent, string property) = payload.Kind switch {
                "original-manifest" when payload.Id is not null => (index["Originals"]![payload.Id]!.AsObject(), "Manifest"),
                "cycle-manifest" when payload.Id is null => (index["Cycle"]!.AsObject(), "Manifest"),
                "owner-admission" when payload.Id is not null => (index["Anchors"]![payload.Id]!.AsObject(), "Admitted"),
                "registration" when payload.Id is not null => (index["Registrations"]!.AsObject(), payload.Id),
                _ => throw new AuthorityUnavailableException("Unknown checkpoint payload kind."),
            };
            if (parent[property] is not null) throw new AuthorityUnavailableException("Checkpoint payload substitution.");
            parent[property] = JsonNode.Parse(loaded[payload.Object]);
        }
        return CustodyJson.Read<Checkpoint>(JsonSerializer.SerializeToUtf8Bytes(index, CustodyBindingContract.Json));
    }
}
