using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact retained owner source fact, measured by a typed independently authenticated reader.</summary>
public sealed record QualificationObservedOriginal(string RequestId,string OriginalReceiptSha256,string AuthorityRevision,JsonObject Source,string? ObservationIdentity=null,string? ObservationVersion=null,long ObservedAt=0,long ValidUntil=0,byte[]? Signature=null);
