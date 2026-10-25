using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Verified control original. A process or restore operation cannot be replaced by an owner success flag.</summary>
public sealed record QualificationRecoveryControlOriginal(string RequestId,string OperationId,string Operation,string TargetCommit,
    string IsolatedTargetId,string BeforeIncarnation,string AfterIncarnation,string OriginalReceiptSha256,string ControlReceiptSha256,byte[]? Signature=null);
