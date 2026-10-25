namespace Hexalith.Platform.Composition;

using Hexalith.Platform.Custody;

/// <summary>Private denial capture and trusted retained server-receipt identity, supplied by the owning ingress before the first observation. No client field supplies the original digest version.</summary>
public sealed record AgentsIngressDenialContext(PlatformSecurityDenialRecorder Recorder,string RetainedServerReceiptId,
    string OriginalDigestKeyVersion,IReadOnlyList<string?> UntrustedFields);
