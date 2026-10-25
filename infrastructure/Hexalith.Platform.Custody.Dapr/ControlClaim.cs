using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record ControlClaim(string ControlProofSha256, string GuardOriginalSha256, string ProtectionOriginalSha256,
    string CurrentRevocationSha256, long CurrentGuardRevision, long? EffectiveDispatchRevision, string? EffectiveDispatchReceiptId,
    HoldReservation? HoldReservation = null);
