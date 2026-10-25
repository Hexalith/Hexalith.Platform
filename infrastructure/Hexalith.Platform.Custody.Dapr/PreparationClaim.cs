using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record PreparationClaim(string ManifestDigest, string SourceEpoch, string SuccessorEpoch,
    string InventoryDigest, string SurvivorCheckpointDigest, string ProducerClosureDigest, string HolderClosureDigest,
    string[] PermanentlyDeletedAlternateEpochs, string[] SurvivorOpaqueReferences, string[] TargetOpaqueReferences,
    string NativeIntegrityEvidenceDigest, string RestoreEvidenceDigest);
