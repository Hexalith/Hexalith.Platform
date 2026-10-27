using Dapr.Actors.Runtime;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit private actor registration and native release/current-authority ports. These capabilities are constructor captures, never general DI services.</summary>
public sealed record PrivateExportKeyDeliveryOwner(ActorRegistrationCollection Actors,IExportKeyDeliveryAuthority Authority,IExportKeyDirectDeliveryProvider? Provider);
