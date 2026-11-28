namespace Hexalith.Platform.Composition;

/// <summary>One relay response: an upstream gateway answer returned unchanged, or a local refusal without a body.</summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="ContentType">The upstream content type, when any.</param>
/// <param name="Body">The bounded body bytes.</param>
internal sealed record PlatformOperatorRelayResponse(int Status, string? ContentType, byte[] Body)
{
    /// <summary>Creates a local response that forwards nothing and discloses nothing.</summary>
    internal static PlatformOperatorRelayResponse Empty(int status) => new(status, null, []);
}
