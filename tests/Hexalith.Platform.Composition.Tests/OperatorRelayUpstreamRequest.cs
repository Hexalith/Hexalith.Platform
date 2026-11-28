namespace Hexalith.Platform.Composition.Tests;

/// <summary>One request the operator relay sent upstream.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The absolute request path.</param>
/// <param name="Headers">The request headers, by lower-case name.</param>
/// <param name="Body">The request body text.</param>
internal sealed record OperatorRelayUpstreamRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string Body);
