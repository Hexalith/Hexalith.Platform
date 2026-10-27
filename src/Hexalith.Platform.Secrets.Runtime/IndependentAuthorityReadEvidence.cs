using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact public request and independently signed lookup response, retained at its original verification time.</summary>
public sealed record IndependentAuthorityReadEvidence(JsonElement Request,IndependentAuthoritySignedResponse Response);
