using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Transport observation. <see cref="CustodyBindingTransportState.ReceivedUnverified"/> is not a destruction receipt.</summary>
/// <param name="Transport">Refused, unknown, or unverified.</param>
/// <param name="Outcome">Component observation, still unverified until the outside witness confirms it.</param>
/// <param name="Wrote">True only when the disposable store committed a mutation.</param>
internal sealed record CustodyLifecycleBindingReply(CustodyBindingTransportState Transport, CustodyComponentOutcome? Outcome, bool Wrote);
