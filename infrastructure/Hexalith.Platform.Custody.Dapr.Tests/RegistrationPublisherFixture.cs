using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

/// <summary>Disposable publication double; establishes no independent installation or physical guarantee.</summary>
internal sealed class RegistrationPublisherFixture : ICustodyRegistrationPublisher
{
    internal readonly Dictionary<CustodyKeyObjectIdentity, CustodyKeyRegistration> Originals = [];
    internal bool Unavailable;
    internal bool LoseAcknowledgement;
    internal bool Substitute;
    internal Func<CustodyKeyRegistration, Task>? BeforePublication;

    /// <inheritdoc />
    public async Task<CustodyKeyRegistration?> PublishAsync(CustodyKeyRegistration original, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BeforePublication is { } action) await action(original);
        if (Unavailable) return null;
        if (Originals.TryGetValue(original.Identity, out var prior) && prior != original) return null;
        Originals[original.Identity] = original;
        if (LoseAcknowledgement) { LoseAcknowledgement = false; return null; }
        return Substitute ? original with { WrapReceiptId = "substituted" } : original;
    }
}
