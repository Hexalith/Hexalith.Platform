namespace Hexalith.Platform.Custody.Dapr;

// Explicit capability/resource enrollment. Metadata alone cannot prove that a selected server supports AEAD associated_data.
internal sealed record OpenBaoTransitBindingProfile(string Schema,bool Derived);
