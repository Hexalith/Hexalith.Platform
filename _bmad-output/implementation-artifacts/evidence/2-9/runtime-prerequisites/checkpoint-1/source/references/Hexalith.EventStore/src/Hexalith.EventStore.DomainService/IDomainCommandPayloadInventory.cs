namespace Hexalith.EventStore.DomainService;

/// <summary>Exact finite payload allowlist enforced by a custom domain command processor.</summary>
public interface IDomainCommandPayloadInventory
{
    /// <summary>Gets the actual payload types accepted by this processor, including explicit non-aggregate routes.</summary>
    IReadOnlyList<Type> CommandPayloadTypes { get; }
}
