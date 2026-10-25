using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record QualificationClaim(string ProfileDigest, string ArtifactDigest, string TransportProfileDigest,
    string ClosureProfileDigest, string CapacityMeasurementDigest, long CompletionReserveBytes,
    string IndependentPlacementEvidenceDigest, string NativeAuthorizationDigest);
