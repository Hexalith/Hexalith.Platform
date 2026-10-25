using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual independently retained native receipt history and observed leakage surfaces for an original. Reader implementations must inspect their installed native source.</summary>
public sealed record QualificationNativeCaseAudit(long OriginalRevision,JsonArray Receipts,JsonArray TargetMutations,JsonArray SurfaceScans,JsonArray CopyCensus,JsonObject? Facts=null);
