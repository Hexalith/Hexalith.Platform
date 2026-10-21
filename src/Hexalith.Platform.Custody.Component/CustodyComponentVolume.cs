using System.Security.Cryptography;
using System.Text;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Restorable volume. Epoch bait and envelopes live here. The witness does not.</summary>
internal sealed class CustodyComponentVolume
{
    private readonly Dictionary<string, byte[]> _epochs = new(StringComparer.Ordinal);
    private readonly List<CustodyComponentEnvelope> _envelopes = [];

    internal IReadOnlyCollection<string> EpochGenerations => _epochs.Keys;
    internal IReadOnlyList<CustodyComponentEnvelope> Envelopes => _envelopes;

    internal void AddEpoch(string generation, byte[] key)
    {
        if (_epochs.ContainsKey(generation)) { throw new InvalidOperationException("Epoch generation already stored."); }
        _epochs.Add(generation, key);
    }

    internal bool HoldsEpochBytes(string generation)
        => _epochs.TryGetValue(generation, out byte[]? key) && key.Any(b => b != 0);

    internal CustodyComponentEnvelope? Find(CustodyKeyObjectIdentity identity)
        => _envelopes.SingleOrDefault(e => e.Identity == identity);

    internal void Add(CustodyComponentEnvelope envelope)
    {
        if (_envelopes.Any(e => e.Identity == envelope.Identity)) { throw new InvalidOperationException("Envelope already stored."); }
        _envelopes.Add(envelope);
    }

    internal void ZeroEpoch(string generation)
    {
        if (_epochs.TryGetValue(generation, out byte[]? key)) { CryptographicOperations.ZeroMemory(key); }
    }

    internal void ZeroSecrets()
    {
        foreach (byte[] key in _epochs.Values) { CryptographicOperations.ZeroMemory(key); }
        foreach (var envelope in _envelopes) { envelope.Zero(); }
    }

    internal void ZeroEnvelope(CustodyKeyObjectIdentity identity) => Find(identity)?.Zero();

    internal byte[] Capture()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(0x48584356); // HXCV
        writer.Write(1);
        writer.Write(_epochs.Count);
        foreach (var pair in _epochs.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.Write(pair.Key);
            writer.Write(pair.Value.Length);
            writer.Write(pair.Value);
        }
        writer.Write(_envelopes.Count);
        foreach (var envelope in _envelopes)
        {
            WriteIdentity(writer, envelope.Identity);
            writer.Write(envelope.KekVersion);
            writer.Write(envelope.SealedGeneration);
            writer.Write(envelope.WrapGeneration);
            Write(writer, envelope.OuterNonce);
            Write(writer, envelope.OuterCiphertext);
            Write(writer, envelope.OuterTag);
            Write(writer, envelope.WrappedKeyNonce);
            Write(writer, envelope.WrappedKeyCiphertext);
            Write(writer, envelope.WrappedKeyTag);
        }
        writer.Flush();
        return stream.ToArray();
    }

    internal static CustodyComponentVolume FromSnapshot(byte[] snapshot)
    {
        using var stream = new MemoryStream(snapshot, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadInt32() != 0x48584356 || reader.ReadInt32() != 1) { throw new ArgumentException("Unrecognized custody volume."); }
        var volume = new CustodyComponentVolume();
        int epochs = reader.ReadInt32();
        for (int i = 0; i < epochs; i++)
        {
            string generation = reader.ReadString();
            int length = reader.ReadInt32();
            volume.AddEpoch(generation, reader.ReadBytes(length));
        }
        int envelopes = reader.ReadInt32();
        for (int i = 0; i < envelopes; i++)
        {
            volume.Add(new CustodyComponentEnvelope(ReadIdentity(reader), reader.ReadString(), reader.ReadString(), reader.ReadString(),
                Read(reader), Read(reader), Read(reader), Read(reader), Read(reader), Read(reader)));
        }
        if (stream.Position != stream.Length) { throw new ArgumentException("Trailing custody volume bytes."); }
        return volume;
    }

    internal CustodyComponentVolume Clone()
    {
        var copy = new CustodyComponentVolume();
        foreach (var pair in _epochs) { copy._epochs.Add(pair.Key, (byte[])pair.Value.Clone()); }
        foreach (var envelope in _envelopes) { copy._envelopes.Add(envelope.Clone()); }
        return copy;
    }

    private static void Write(BinaryWriter writer, byte[] bytes)
    {
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static byte[] Read(BinaryReader reader) => reader.ReadBytes(reader.ReadInt32());

    private static void WriteIdentity(BinaryWriter writer, CustodyKeyObjectIdentity identity)
    {
        writer.Write(identity.TenantId);
        writer.Write(identity.ObjectId);
        writer.Write((int)identity.Purpose);
        writer.Write(identity.KeyAlias);
        writer.Write(identity.KeyVersion);
        writer.Write(identity.LifecycleVersion);
        writer.Write(identity.StoreTarget);
        writer.Write(identity.ContractVersion);
    }

    private static CustodyKeyObjectIdentity ReadIdentity(BinaryReader reader)
        => new(reader.ReadString(), reader.ReadString(), (PlatformKeyPurpose)reader.ReadInt32(), reader.ReadString(), reader.ReadString(),
            reader.ReadString(), reader.ReadString(), reader.ReadString());
}
