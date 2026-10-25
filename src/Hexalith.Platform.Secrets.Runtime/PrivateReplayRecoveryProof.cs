using System.Text.Json;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact terminal-no-event native proof for one retained replay original. Every receiving use independently rereads status and confirming native source before admitting a new deterministic transport.</summary>
public sealed record PrivateReplayRecoveryProof(TrustedEnvelopeReplayIntent Intent,string OriginalRequestId,string PreviousTransportMessageId,JsonElement OriginalCommand,SecurityEventNoRecordProof NativeProof);
