using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact independently enrolled custody canary registration, holder identity and original lifecycle reservation. No root bytes or expected outcomes are accepted.</summary>
public sealed record QualificationLifecycleCaseInput(CustodyKeyRegistration Registration,string EnrolledScopeSha256,
    CustodyKeyUse? KeyUse=null,CustodyKeyLifecycleRequest? Lifecycle=null,DeletionBatchConsumptionRequest? Manifest=null,string? ReservationReceiptId=null);
