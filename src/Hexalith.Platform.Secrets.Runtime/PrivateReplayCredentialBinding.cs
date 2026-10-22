namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Private replay credential captured for one registrar. An absent credential, an endpoint, or a stale revision is not installed.</summary>
internal sealed class PrivateReplayCredentialBinding
{
    private PrivateReplayCredentialBinding(string reference, long revision, bool current)
    {
        Reference = reference;
        Revision = revision;
        IsCurrent = current && revision > 0;
    }

    internal string Reference { get; }
    internal long Revision { get; }
    internal bool IsCurrent { get; }

    /// <summary>Captures a credential that is distinct from the registrar endpoint. A null, endpoint, or non-positive revision captures nothing.</summary>
    internal static PrivateReplayCredentialBinding? TryCapture(string? endpoint, string? credentialReference, long revision)
    {
        if (revision <= 0 || !SecretsProvisioning.IsReference(credentialReference)) { return null; }
        if (IsEndpoint(credentialReference!) || string.Equals(credentialReference, endpoint, StringComparison.Ordinal)) { return null; }
        return new(credentialReference!, revision, true);
    }

    /// <summary>Same captured reference with current permission withdrawn.</summary>
    internal PrivateReplayCredentialBinding AsStale() => new(Reference, Revision, false);

    private static bool IsEndpoint(string value)
    {
        if (value.Contains("://", StringComparison.Ordinal)) { return true; }
        if (value.StartsWith("file:/", StringComparison.Ordinal)) { return false; }
        int colon = value.IndexOf(':');
        return colon > 0 && colon < value.Length - 1 && value[(colon + 1)..].All(char.IsDigit);
    }
}
