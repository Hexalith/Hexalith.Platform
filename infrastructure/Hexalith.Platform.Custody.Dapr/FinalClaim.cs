using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record FinalClaim(string ManifestDigest, string SourceEpoch, string SuccessorEpoch, string PreparationDigest,
    string[] PermanentlyDeletedEpochs, string InventoryDigest, string HolderClosureDigest, string SurvivorRecoveryDigest,
    string RestoreBarrierReceiptId, string NativeFinalEvidenceDigest, string OriginalSha256);
