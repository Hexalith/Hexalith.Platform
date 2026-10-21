using Hexalith.Platform.Custody.Component;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

/// <summary>Returns a claimed outcome without writing. Transport success is not a witness receipt.</summary>
internal sealed class RecordingCustodyBinding : ICustodyLifecycleBinding
{
    internal List<CustodyLifecycleBindingCommand> Commands { get; } = [];
    internal CustodyComponentOutcome? Claim { get; set; }

    public bool Present { get; set; } = true;

    public Task<CustodyLifecycleBindingReply> DispatchAsync(CustodyLifecycleBindingCommand command, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        var outcome = Claim ?? new CustodyComponentOutcome(CustodyComponentStatus.Destroyed, command.Lifecycle?.OperationId ?? "claimed", command.RequestDigest, "fake-receipt", "fake-barrier");
        return Task.FromResult(new CustodyLifecycleBindingReply(CustodyBindingTransportState.ReceivedUnverified, outcome, false));
    }
}
