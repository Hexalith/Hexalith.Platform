using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact independently enrolled principal delivery original. No transport, approval or actor-revocation evidence is minted by this input.</summary>
public sealed record QualificationExportDeliveryInput(ExportKeyDeliveryIdentity Original,string EnrolledScopeSha256);
