namespace Hexalith.Platform.Custody;

/// <summary>Actual native source prefix and independently retained submission/terminal-status facts establishing no recorded observation.</summary>
public sealed record SecurityEventNoRecordProof(string OriginalMessageId,string CurrentTransportMessageId,string RetainedOriginalSha256,
    string NativeSourceIncarnation,long NativeHead,string NativePrefixSha256,string? TerminalStatusSha256,string Basis);
