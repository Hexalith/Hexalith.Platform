namespace Hexalith.Platform.Custody.Dapr;

internal sealed record NativeCompletionReservation(string Tenant,string OperationId,long RemainingBytes);
