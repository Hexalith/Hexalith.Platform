namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently enrolled current measured route capacity, including actual Dapr request/invoke, receiver and response limits. Source defaults and fixture measurements establish no installed capacity.</summary>
public sealed record IndependentAuthorityTransportCapacity(string InstallationRevision,string DaprSidecarIdentity,string MeasurementOriginalSha256,
    long MaximumDaprRequestBytes,long MaximumDaprInvokeBytes,long MaximumReceiverBytes,long MaximumResponseBytes,DateTimeOffset ValidUntil);
