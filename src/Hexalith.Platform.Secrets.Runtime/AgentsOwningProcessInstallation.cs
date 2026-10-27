using Dapr.Client;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit independently installed private owner ports, consumed inside the Agents process, never its AppHost.</summary>
public sealed record AgentsOwningProcessInstallation(IndependentDaprAuthorityBinding SpoolAuthority,
    IndependentDaprAuthorityBinding Recorder, IndependentDaprAuthorityBinding ReplayAuthority,
    SecretsRuntimeConfiguration Secrets, DeletionCapabilityRevocationSubscription CompromiseSubscription, IndependentDaprAuthorityBinding ProtectionEngine,
    IndependentDaprAuthorityBinding ProtectionObserver, Fr34ProtectionTarget ProtectionTarget,
    TimeSpan DrainInterval, int DrainMaximumCount,IndependentDaprAuthorityBinding? ExportDeliveryDenialOriginals=null);
