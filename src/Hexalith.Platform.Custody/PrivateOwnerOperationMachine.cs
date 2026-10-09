namespace Hexalith.Platform.Custody;

/// <summary>Owned bounded snapshot of the four exact authenticated machine claim values.</summary>
internal sealed record PrivateOwnerOperationMachine(string Issuer, string Subject, string Client, string Audience);
