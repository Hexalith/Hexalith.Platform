namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Durable conditional reservation before large immutable publication. Conflicting request/result bodies never consume another admitted completion's reserve.</summary>
public sealed record IndependentOriginalPublication(string Role,string RequestId,string Kind,string BodySha256,long ReservedBytes,long CompletionBytes,bool FinalAtAdmission=false);
