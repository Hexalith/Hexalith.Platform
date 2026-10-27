using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Versioned content-free complete-command binding. Never retains reversible canary payload bytes.</summary>
public static class Fr34CommandFingerprint
{
    /// <summary>The safe independent original format; legacy raw inputs remain preserved and unsafe for qualification.</summary>
    public const string Protocol="fr34-command-fingerprint-v2";
    /// <summary>Fingerprints every command field and its authenticated principal, including payload and extensions.</summary>
    public static JsonElement Create(SubmitCommandRequest command,string principal)=>JsonSerializer.SerializeToElement(new
    {
        protocol=Protocol,
        commandSha256=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(command,CustodyBindingContract.Json)!),
        principalSha256=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(principal,CustodyBindingContract.Json)!)
    },CustodyBindingContract.Json);
    /// <summary>Checks the transient complete command against the exact retained descriptor before dispatch.</summary>
    public static bool Matches(JsonElement original,SubmitCommandRequest command,string principal)
        =>IsSafe(original,principal) && JsonElement.DeepEquals(original,Create(command,principal));
    /// <summary>Rejects unsafe legacy plaintext originals without rewriting or deleting them.</summary>
    public static bool IsSafe(JsonElement original,string principal)
        =>IsDescriptor(original) && original.GetProperty("principalSha256").GetString()==QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(principal,CustodyBindingContract.Json)!);
    /// <summary>Checks the persisted descriptor format without material or workload authority.</summary>
    public static bool IsDescriptor(JsonElement original)
        =>original.ValueKind==JsonValueKind.Object && original.EnumerateObject().Count()==3
        && original.TryGetProperty("protocol",out var version) && version.ValueKind==JsonValueKind.String && version.GetString()==Protocol
        && original.TryGetProperty("commandSha256",out var command) && command.ValueKind==JsonValueKind.String
        && command.GetString() is { Length:64 } digest && digest.All(char.IsAsciiHexDigit)
        && original.TryGetProperty("principalSha256",out var owner)
        && owner.ValueKind==JsonValueKind.String && owner.GetString() is { Length:64 } ownerDigest && ownerDigest.All(char.IsAsciiHexDigit);
    /// <summary>Independent unseal comparison reconstitutes the original transient body and verifies its complete command digest.</summary>
    public static bool MatchesCommand(JsonElement original,SubmitCommandRequest command)
        =>IsDescriptor(original) && original.GetProperty("commandSha256").GetString()==QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(command,CustodyBindingContract.Json)!);
}
