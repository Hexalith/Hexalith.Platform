using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Measured guard/protection/signing invariants and precise unsupported independent read protocols. No expected metric values are inputs.</summary>
public sealed record QualificationInvariantProjection(IReadOnlyDictionary<string,long> Metrics,IReadOnlyList<string> MissingProtocols);
