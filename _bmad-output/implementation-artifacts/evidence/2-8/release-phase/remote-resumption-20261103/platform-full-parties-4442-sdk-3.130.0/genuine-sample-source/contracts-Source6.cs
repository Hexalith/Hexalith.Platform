using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Sample.Counter.Queries;

/// <summary>The exact public payload produced by the existing counter projection.</summary>
/// <param name="Count">The projected counter value.</param>
public sealed record CounterStatusPayload([property: JsonPropertyName("count")] int Count);
