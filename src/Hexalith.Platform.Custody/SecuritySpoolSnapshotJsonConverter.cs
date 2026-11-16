using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Custody;

/// <summary>Preserves the exact pre-reservation or current snapshot member layout used by independent anchors and archive links.</summary>
public sealed class SecuritySpoolSnapshotJsonConverter : JsonConverter<SecuritySpoolSnapshot>
{
    /// <inheritdoc />
    public override SecuritySpoolSnapshot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) { throw new JsonException("Malformed spool snapshot."); }
        var members = new Dictionary<string, JsonElement>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!members.TryAdd(property.Name, property.Value)) { throw new JsonException("Duplicate spool snapshot member."); }
        }
        bool digestPresent = members.ContainsKey(Name(nameof(SecuritySpoolSnapshot.ReservedArchiveDigest)));
        bool countPresent = members.ContainsKey(Name(nameof(SecuritySpoolSnapshot.ArchiveReservationCount)));
        if (digestPresent != countPresent) { throw new JsonException("Partial spool reservation layout."); }
        string? reserved = digestPresent ? Value<string?>(nameof(SecuritySpoolSnapshot.ReservedArchiveDigest)) : null;
        long count = countPresent ? Value<long>(nameof(SecuritySpoolSnapshot.ArchiveReservationCount)) : 0;
        if (count < 0 || reserved is not null && (reserved.Length != 64 || reserved.Any(static character => !char.IsAsciiHexDigit(character))))
        { throw new JsonException("Malformed spool reservation."); }
        var snapshot = new SecuritySpoolSnapshot(Value<string>(nameof(SecuritySpoolSnapshot.InstallationEpoch)), Value<long>(nameof(SecuritySpoolSnapshot.Revision)),
            Value<IReadOnlyList<SecurityObservationRecord>>(nameof(SecuritySpoolSnapshot.Records)))
        {
            PageIndex = Value<long>(nameof(SecuritySpoolSnapshot.PageIndex)),
            ArchivedObservedCount = Value<long>(nameof(SecuritySpoolSnapshot.ArchivedObservedCount)),
            ArchivedAcknowledgedCount = Value<long>(nameof(SecuritySpoolSnapshot.ArchivedAcknowledgedCount)),
            ArchiveHeadDigest = Value<string?>(nameof(SecuritySpoolSnapshot.ArchiveHeadDigest)),
            DrainRevision = Value<long>(nameof(SecuritySpoolSnapshot.DrainRevision)),
            DrainAfterSequence = Value<long>(nameof(SecuritySpoolSnapshot.DrainAfterSequence)),
            ReservedArchiveDigest = reserved,
            ArchiveReservationCount = count,
            UsesReservationLayout = digestPresent,
        };
        // The legacy revision formula has one surplus only during an archive
        // reservation. Derive its metadata without expanding the retained wire head.
        return ReplicatedSecurityObservationSpool.RestoreLegacyReservation(snapshot);

        string Name(string name) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
        T Value<T>(string name) => members.TryGetValue(Name(name), out JsonElement value)
            ? value.Deserialize<T>(options)! : throw new JsonException($"Missing spool member {name}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SecuritySpoolSnapshot value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        writer.WriteStartObject();
        Write(nameof(value.InstallationEpoch), value.InstallationEpoch);
        Write(nameof(value.Revision), value.Revision);
        Write(nameof(value.Records), value.Records);
        Write(nameof(value.PageIndex), value.PageIndex);
        Write(nameof(value.ArchivedObservedCount), value.ArchivedObservedCount);
        Write(nameof(value.ArchivedAcknowledgedCount), value.ArchivedAcknowledgedCount);
        Write(nameof(value.ArchiveHeadDigest), value.ArchiveHeadDigest);
        Write(nameof(value.DrainRevision), value.DrainRevision);
        Write(nameof(value.DrainAfterSequence), value.DrainAfterSequence);
        if (value.UsesReservationLayout)
        {
            Write(nameof(value.ReservedArchiveDigest), value.ReservedArchiveDigest);
            Write(nameof(value.ArchiveReservationCount), value.ArchiveReservationCount);
        }
        writer.WriteEndObject();

        void Write<T>(string name, T member)
        {
            writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(name) ?? name);
            JsonSerializer.Serialize(writer, member, options);
        }
    }
}
