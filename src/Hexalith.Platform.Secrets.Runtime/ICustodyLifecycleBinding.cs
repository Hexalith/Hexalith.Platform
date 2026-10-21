namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Role <c>interaction-custody</c> on <c>custody-lifecycle-v1</c>. Absent when the logical binding is not configured.</summary>
internal interface ICustodyLifecycleBinding
{
    /// <summary>False when no component is bound. Callers must not dispatch.</summary>
    bool Present { get; }

    /// <summary>Sends one operation. Lookup implementations must not start an effect.</summary>
    Task<CustodyLifecycleBindingReply> DispatchAsync(CustodyLifecycleBindingCommand command, CancellationToken cancellationToken);
}
