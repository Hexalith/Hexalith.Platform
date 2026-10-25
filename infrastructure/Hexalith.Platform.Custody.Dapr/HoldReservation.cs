using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

// The control owner durably excludes dispatch until this exact reservation is reconciled. It cannot
// expire on a timer or be canceled from a local app restore. Its signed current claim is not a read-then-write check.
internal sealed record HoldReservation(string ReservationId, string HoldId, string TargetDigest, long RegisteredGuardRevision,
    string RegisteredGuardReceiptId, string State);
